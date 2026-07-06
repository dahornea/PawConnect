using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class VolunteerTaskService(
    ApplicationDbContext context,
    INotificationService? notificationService = null,
    IAuditLogService? auditLogService = null) : IVolunteerTaskService
{
    private const int TitleMaxLength = 160;
    private const int DescriptionMaxLength = 1000;
    private const int NotesMaxLength = 1000;
    private const int RequiredSkillsMaxLength = 500;
    private const int LocationMaxLength = 250;

    private static readonly VolunteerTaskStatus[] ActiveAssignmentStatuses =
    [
        VolunteerTaskStatus.Assigned,
        VolunteerTaskStatus.InProgress
    ];

    public async Task<VolunteerProfileDto?> GetVolunteerProfileForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId);

        var profile = await context.VolunteerProfiles
            .Include(volunteer => volunteer.PreferredShelter)
            .AsNoTracking()
            .FirstOrDefaultAsync(volunteer => volunteer.UserId == userId, cancellationToken);

        return profile is null ? null : ToProfileDto(profile);
    }

    public async Task<IReadOnlyList<VolunteerProfileDto>> GetVolunteersForShelterAsync(
        int shelterId,
        CancellationToken cancellationToken = default)
    {
        var volunteers = await context.VolunteerProfiles
            .Include(volunteer => volunteer.PreferredShelter)
            .Where(volunteer => volunteer.IsActive &&
                (!volunteer.PreferredShelterId.HasValue || volunteer.PreferredShelterId == shelterId))
            .OrderBy(volunteer => volunteer.DisplayName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return volunteers.Select(ToProfileDto).ToList();
    }

    public async Task<IReadOnlyList<VolunteerProfileDto>> GetAllVolunteersAsync(CancellationToken cancellationToken = default)
    {
        var volunteers = await context.VolunteerProfiles
            .Include(volunteer => volunteer.PreferredShelter)
            .OrderByDescending(volunteer => volunteer.IsActive)
            .ThenBy(volunteer => volunteer.DisplayName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return volunteers.Select(ToProfileDto).ToList();
    }

    public async Task<VolunteerProfileDto> CreateVolunteerProfileAsync(
        VolunteerProfileCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(request.UserId);
        await EnsureUserExistsAsync(request.UserId, cancellationToken);
        await EnsureShelterExistsAsync(request.PreferredShelterId, cancellationToken);

        var existingProfile = await context.VolunteerProfiles
            .AnyAsync(profile => profile.UserId == request.UserId, cancellationToken);
        if (existingProfile)
        {
            throw new InvalidOperationException("This user already has a volunteer profile.");
        }

        var now = DateTime.UtcNow;
        var profile = new VolunteerProfile
        {
            UserId = request.UserId,
            DisplayName = NormalizeRequired(request.DisplayName, "Volunteer name is required.", 120),
            Email = NormalizeRequired(request.Email, "Volunteer email is required.", 256),
            PhoneNumber = NormalizeOptional(request.PhoneNumber, 40),
            PreferredShelterId = request.PreferredShelterId,
            Skills = NormalizeOptional(request.Skills, NotesMaxLength),
            AvailabilityNotes = NormalizeOptional(request.AvailabilityNotes, NotesMaxLength),
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        context.VolunteerProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);
        await LogAsync(AuditActions.VolunteerProfileCreated, "VolunteerProfile", profile.Id.ToString(), $"Volunteer profile created for {profile.DisplayName}.", request.UserId);

        return ToProfileDto(await LoadVolunteerProfileAsync(profile.Id, cancellationToken));
    }

    public async Task<VolunteerProfileDto> UpdateVolunteerProfileAsync(
        int volunteerProfileId,
        VolunteerProfileUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureShelterExistsAsync(request.PreferredShelterId, cancellationToken);

        var profile = await context.VolunteerProfiles
            .FirstOrDefaultAsync(profile => profile.Id == volunteerProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Volunteer profile was not found.");

        profile.DisplayName = NormalizeRequired(request.DisplayName, "Volunteer name is required.", 120);
        profile.Email = NormalizeRequired(request.Email, "Volunteer email is required.", 256);
        profile.PhoneNumber = NormalizeOptional(request.PhoneNumber, 40);
        profile.PreferredShelterId = request.PreferredShelterId;
        profile.Skills = NormalizeOptional(request.Skills, NotesMaxLength);
        profile.AvailabilityNotes = NormalizeOptional(request.AvailabilityNotes, NotesMaxLength);
        profile.IsActive = request.IsActive;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await LogAsync(AuditActions.VolunteerProfileUpdated, "VolunteerProfile", profile.Id.ToString(), $"Volunteer profile updated for {profile.DisplayName}.", profile.UserId);

        return ToProfileDto(await LoadVolunteerProfileAsync(profile.Id, cancellationToken));
    }

    public async Task<VolunteerTaskDto> CreateTaskAsync(
        int shelterId,
        string createdByUserId,
        VolunteerTaskCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(createdByUserId);
        await EnsureShelterExistsAsync(shelterId, cancellationToken);
        await EnsureDogBelongsToShelterAsync(request.DogId, shelterId, cancellationToken);
        ValidateSchedule(request.ScheduledStartUtc, request.ScheduledEndUtc);

        var now = DateTime.UtcNow;
        var task = new VolunteerTask
        {
            ShelterId = shelterId,
            DogId = request.DogId,
            CreatedByUserId = createdByUserId,
            Title = NormalizeRequired(request.Title, "Task title is required.", TitleMaxLength),
            Description = NormalizeOptional(request.Description, DescriptionMaxLength),
            Category = request.Category,
            Priority = request.Priority,
            ScheduledStartUtc = ToUtc(request.ScheduledStartUtc),
            ScheduledEndUtc = ToUtc(request.ScheduledEndUtc),
            DueAtUtc = ToUtcOrNull(request.DueAtUtc),
            Location = NormalizeOptional(request.Location, LocationMaxLength),
            RequiredSkills = NormalizeOptional(request.RequiredSkills, RequiredSkillsMaxLength),
            ShelterNotes = NormalizeOptional(request.ShelterNotes, NotesMaxLength),
            Status = VolunteerTaskStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        if (request.AssignedVolunteerProfileId.HasValue)
        {
            await AssignVolunteerCoreAsync(task, request.AssignedVolunteerProfileId.Value, createdByUserId, "Task created and assigned.", cancellationToken);
        }

        AddActivity(task, VolunteerTaskActivityType.Created, createdByUserId, "Volunteer task created.");
        context.VolunteerTasks.Add(task);
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTaskAsync(task.Id, cancellationToken);
        await NotifyVolunteerAssignedAsync(saved);
        await LogAsync(AuditActions.VolunteerTaskCreated, "VolunteerTask", saved.Id.ToString(), $"Volunteer task created: {saved.Title}.", createdByUserId);

        return ToTaskDto(saved, shelterId, volunteerProfileId: null, isAdmin: false);
    }

    public async Task<VolunteerTaskDetailsDto> UpdateTaskAsync(
        int taskId,
        int shelterId,
        string updatedByUserId,
        VolunteerTaskUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(updatedByUserId);
        ValidateSchedule(request.ScheduledStartUtc, request.ScheduledEndUtc);

        var task = await LoadTaskForUpdateAsync(taskId, cancellationToken);
        EnsureShelterOwnsTask(task, shelterId);
        if (task.Status is VolunteerTaskStatus.Completed or VolunteerTaskStatus.Cancelled)
        {
            throw new InvalidOperationException("Completed or cancelled volunteer tasks cannot be edited.");
        }

        await EnsureDogBelongsToShelterAsync(request.DogId, shelterId, cancellationToken);
        task.DogId = request.DogId;
        task.Title = NormalizeRequired(request.Title, "Task title is required.", TitleMaxLength);
        task.Description = NormalizeOptional(request.Description, DescriptionMaxLength);
        task.Category = request.Category;
        task.Priority = request.Priority;
        task.ScheduledStartUtc = ToUtc(request.ScheduledStartUtc);
        task.ScheduledEndUtc = ToUtc(request.ScheduledEndUtc);
        task.DueAtUtc = ToUtcOrNull(request.DueAtUtc);
        task.Location = NormalizeOptional(request.Location, LocationMaxLength);
        task.RequiredSkills = NormalizeOptional(request.RequiredSkills, RequiredSkillsMaxLength);
        task.ShelterNotes = NormalizeOptional(request.ShelterNotes, NotesMaxLength);
        task.UpdatedAtUtc = DateTime.UtcNow;

        await EnsureNoVolunteerOverlapAsync(task, cancellationToken);
        AddActivity(task, VolunteerTaskActivityType.CommentAdded, updatedByUserId, "Task details updated.");
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTaskAsync(task.Id, cancellationToken);
        await LogAsync(AuditActions.VolunteerTaskUpdated, "VolunteerTask", saved.Id.ToString(), $"Volunteer task updated: {saved.Title}.", updatedByUserId);
        return ToDetailsDto(saved, shelterId, volunteerProfileId: null, isAdmin: false);
    }

    public async Task<VolunteerTaskDetailsDto> AssignTaskAsync(
        int taskId,
        int shelterId,
        string assignedByUserId,
        VolunteerTaskAssignRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(assignedByUserId);
        var task = await LoadTaskForUpdateAsync(taskId, cancellationToken);
        EnsureShelterOwnsTask(task, shelterId);
        if (task.Status is VolunteerTaskStatus.Completed or VolunteerTaskStatus.Cancelled)
        {
            throw new InvalidOperationException("Completed or cancelled volunteer tasks cannot be assigned.");
        }

        if (request.VolunteerProfileId is null)
        {
            task.AssignedVolunteerProfileId = null;
            task.AssignedAtUtc = null;
            task.StartedAtUtc = null;
            task.Status = VolunteerTaskStatus.Open;
            AddActivity(task, VolunteerTaskActivityType.Assigned, assignedByUserId, "Task assignment removed.");
        }
        else
        {
            await AssignVolunteerCoreAsync(task, request.VolunteerProfileId.Value, assignedByUserId, request.Notes, cancellationToken);
        }

        task.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTaskAsync(task.Id, cancellationToken);
        await NotifyVolunteerAssignedAsync(saved);
        await LogAsync(AuditActions.VolunteerTaskAssigned, "VolunteerTask", saved.Id.ToString(), $"Volunteer task assigned: {saved.Title}.", assignedByUserId);
        return ToDetailsDto(saved, shelterId, volunteerProfileId: null, isAdmin: false);
    }

    public async Task<VolunteerTaskDetailsDto> AcceptTaskAsync(
        int taskId,
        string volunteerUserId,
        VolunteerTaskActionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(volunteerUserId);
        var profile = await LoadActiveVolunteerProfileForUserAsync(volunteerUserId, cancellationToken);
        var task = await LoadTaskForUpdateAsync(taskId, cancellationToken);
        EnsureVolunteerCanAccessOpenTask(task, profile);
        if (task.Status != VolunteerTaskStatus.Open)
        {
            throw new InvalidOperationException("Only open volunteer tasks can be accepted.");
        }

        await AssignVolunteerCoreAsync(task, profile.Id, volunteerUserId, request?.Notes ?? "Volunteer accepted this task.", cancellationToken, VolunteerTaskActivityType.Accepted);
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTaskAsync(task.Id, cancellationToken);
        await NotifyShelterAsync(saved, "Volunteer task accepted", $"{profile.DisplayName} accepted volunteer task '{saved.Title}'.", NotificationType.Success);
        await LogAsync(AuditActions.VolunteerTaskAccepted, "VolunteerTask", saved.Id.ToString(), $"Volunteer accepted task: {saved.Title}.", volunteerUserId);
        return ToDetailsDto(saved, shelterId: null, profile.Id, isAdmin: false);
    }

    public async Task<VolunteerTaskDetailsDto> StartTaskAsync(
        int taskId,
        string volunteerUserId,
        VolunteerTaskActionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var (task, profile) = await LoadAssignedVolunteerTaskAsync(taskId, volunteerUserId, cancellationToken);
        if (task.Status != VolunteerTaskStatus.Assigned)
        {
            throw new InvalidOperationException("Only assigned volunteer tasks can be started.");
        }

        task.Status = VolunteerTaskStatus.InProgress;
        task.StartedAtUtc = DateTime.UtcNow;
        task.VolunteerNotes = MergeNotes(task.VolunteerNotes, "Start note", request?.Notes);
        task.UpdatedAtUtc = task.StartedAtUtc.Value;
        AddActivity(task, VolunteerTaskActivityType.Started, volunteerUserId, request?.Notes ?? "Volunteer started this task.");
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTaskAsync(task.Id, cancellationToken);
        await NotifyShelterAsync(saved, "Volunteer task started", $"{profile.DisplayName} started volunteer task '{saved.Title}'.", NotificationType.Info);
        await LogAsync(AuditActions.VolunteerTaskStarted, "VolunteerTask", saved.Id.ToString(), $"Volunteer started task: {saved.Title}.", volunteerUserId);
        return ToDetailsDto(saved, shelterId: null, profile.Id, isAdmin: false);
    }

    public async Task<VolunteerTaskDetailsDto> CompleteTaskAsync(
        int taskId,
        string volunteerUserId,
        VolunteerTaskActionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var (task, profile) = await LoadAssignedVolunteerTaskAsync(taskId, volunteerUserId, cancellationToken);
        if (task.Status != VolunteerTaskStatus.InProgress)
        {
            throw new InvalidOperationException("Only in-progress volunteer tasks can be completed.");
        }

        task.Status = VolunteerTaskStatus.Completed;
        task.CompletedAtUtc = DateTime.UtcNow;
        task.CompletionNotes = NormalizeOptional(request?.Notes, NotesMaxLength);
        task.UpdatedAtUtc = task.CompletedAtUtc.Value;
        AddActivity(task, VolunteerTaskActivityType.Completed, volunteerUserId, request?.Notes ?? "Volunteer completed this task.");
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTaskAsync(task.Id, cancellationToken);
        await NotifyShelterAsync(saved, "Volunteer task completed", $"{profile.DisplayName} completed volunteer task '{saved.Title}'.", NotificationType.Success);
        await LogAsync(AuditActions.VolunteerTaskCompleted, "VolunteerTask", saved.Id.ToString(), $"Volunteer completed task: {saved.Title}.", volunteerUserId);
        return ToDetailsDto(saved, shelterId: null, profile.Id, isAdmin: false);
    }

    public async Task<VolunteerTaskDetailsDto> CancelTaskAsync(
        int taskId,
        int? shelterId,
        string cancelledByUserId,
        bool isAdmin = false,
        VolunteerTaskActionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(cancelledByUserId);
        var task = await LoadTaskForUpdateAsync(taskId, cancellationToken);
        if (!isAdmin)
        {
            EnsureShelterOwnsTask(task, shelterId);
        }

        if (task.Status == VolunteerTaskStatus.Completed)
        {
            throw new InvalidOperationException("Completed volunteer tasks cannot be cancelled.");
        }

        if (task.Status == VolunteerTaskStatus.Cancelled)
        {
            throw new InvalidOperationException("Volunteer task is already cancelled.");
        }

        task.Status = VolunteerTaskStatus.Cancelled;
        task.CancelledAtUtc = DateTime.UtcNow;
        task.ShelterNotes = MergeNotes(task.ShelterNotes, "Cancellation note", request?.Notes);
        task.UpdatedAtUtc = task.CancelledAtUtc.Value;
        AddActivity(task, VolunteerTaskActivityType.Cancelled, cancelledByUserId, request?.Notes ?? "Volunteer task cancelled.");
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTaskAsync(task.Id, cancellationToken);
        await NotifyVolunteerAsync(saved, "Volunteer task cancelled", $"Volunteer task '{saved.Title}' was cancelled.", NotificationType.Warning);
        await LogAsync(AuditActions.VolunteerTaskCancelled, "VolunteerTask", saved.Id.ToString(), $"Volunteer task cancelled: {saved.Title}.", cancelledByUserId);
        return ToDetailsDto(saved, shelterId, volunteerProfileId: null, isAdmin);
    }

    public async Task<VolunteerTaskDetailsDto> ReopenTaskAsync(
        int taskId,
        int? shelterId,
        string reopenedByUserId,
        bool isAdmin = false,
        VolunteerTaskActionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(reopenedByUserId);
        var task = await LoadTaskForUpdateAsync(taskId, cancellationToken);
        if (!isAdmin)
        {
            EnsureShelterOwnsTask(task, shelterId);
        }

        if (task.Status != VolunteerTaskStatus.Cancelled)
        {
            throw new InvalidOperationException("Only cancelled volunteer tasks can be reopened.");
        }

        task.Status = task.AssignedVolunteerProfileId.HasValue ? VolunteerTaskStatus.Assigned : VolunteerTaskStatus.Open;
        task.CancelledAtUtc = null;
        task.ShelterNotes = MergeNotes(task.ShelterNotes, "Reopen note", request?.Notes);
        task.UpdatedAtUtc = DateTime.UtcNow;
        AddActivity(task, VolunteerTaskActivityType.Reopened, reopenedByUserId, request?.Notes ?? "Volunteer task reopened.");
        await EnsureNoVolunteerOverlapAsync(task, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTaskAsync(task.Id, cancellationToken);
        await NotifyVolunteerAssignedAsync(saved);
        await LogAsync(AuditActions.VolunteerTaskReopened, "VolunteerTask", saved.Id.ToString(), $"Volunteer task reopened: {saved.Title}.", reopenedByUserId);
        return ToDetailsDto(saved, shelterId, volunteerProfileId: null, isAdmin);
    }

    public async Task<VolunteerTaskDetailsDto> AddTaskCommentAsync(
        int taskId,
        string userId,
        VolunteerTaskActionRequest request,
        int? shelterId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId);
        var note = NormalizeRequired(request.Notes, "Comment is required.", NotesMaxLength);
        var task = await LoadTaskForUpdateAsync(taskId, cancellationToken);
        var volunteerProfile = await context.VolunteerProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        if (!CanView(task, shelterId, volunteerProfile?.Id, isAdmin))
        {
            throw new InvalidOperationException("Volunteer task was not found or is not visible to this account.");
        }

        AddActivity(task, VolunteerTaskActivityType.CommentAdded, userId, note);
        task.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        var saved = await LoadRequiredTaskAsync(task.Id, cancellationToken);
        await LogAsync(AuditActions.VolunteerTaskCommentAdded, "VolunteerTask", saved.Id.ToString(), $"Comment added to volunteer task: {saved.Title}.", userId);
        return ToDetailsDto(saved, shelterId, volunteerProfile?.Id, isAdmin);
    }

    public async Task<IReadOnlyList<VolunteerTaskDto>> GetShelterTasksAsync(
        int shelterId,
        VolunteerTaskFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = await ApplyTaskFilter(BaseQuery().Where(task => task.ShelterId == shelterId), filter)
            .OrderByDescending(task => task.Status == VolunteerTaskStatus.Open || task.Status == VolunteerTaskStatus.Assigned || task.Status == VolunteerTaskStatus.InProgress)
            .ThenByDescending(task => task.Priority)
            .ThenBy(task => task.ScheduledStartUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return tasks.Select(task => ToTaskDto(task, shelterId, volunteerProfileId: null, isAdmin: false)).ToList();
    }

    public async Task<IReadOnlyList<VolunteerTaskDto>> GetVolunteerTasksAsync(
        string volunteerUserId,
        VolunteerTaskFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadActiveVolunteerProfileForUserAsync(volunteerUserId, cancellationToken);
        var tasks = await ApplyTaskFilter(BaseQuery().Where(task => task.AssignedVolunteerProfileId == profile.Id), filter)
            .OrderByDescending(task => task.Status == VolunteerTaskStatus.Assigned || task.Status == VolunteerTaskStatus.InProgress)
            .ThenBy(task => task.ScheduledStartUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return tasks.Select(task => ToTaskDto(task, shelterId: null, profile.Id, isAdmin: false)).ToList();
    }

    public async Task<IReadOnlyList<VolunteerTaskDto>> GetOpenTasksForVolunteerAsync(
        string volunteerUserId,
        VolunteerTaskFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadActiveVolunteerProfileForUserAsync(volunteerUserId, cancellationToken);
        var query = BaseQuery().Where(task => task.Status == VolunteerTaskStatus.Open);
        if (profile.PreferredShelterId.HasValue)
        {
            query = query.Where(task => task.ShelterId == profile.PreferredShelterId.Value);
        }

        var tasks = await ApplyTaskFilter(query, filter)
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.ScheduledStartUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return tasks.Select(task => ToTaskDto(task, shelterId: null, profile.Id, isAdmin: false)).ToList();
    }

    public async Task<IReadOnlyList<VolunteerTaskDto>> GetAdminTasksAsync(
        VolunteerTaskFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = await ApplyTaskFilter(BaseQuery(), filter)
            .OrderByDescending(task => task.Status == VolunteerTaskStatus.Open || task.Status == VolunteerTaskStatus.Assigned || task.Status == VolunteerTaskStatus.InProgress)
            .ThenByDescending(task => task.Priority)
            .ThenBy(task => task.ScheduledStartUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return tasks.Select(task => ToTaskDto(task, shelterId: null, volunteerProfileId: null, isAdmin: true)).ToList();
    }

    public async Task<VolunteerTaskDetailsDto?> GetTaskDetailsAsync(
        int taskId,
        int? shelterId = null,
        string? volunteerUserId = null,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        int? volunteerProfileId = null;
        if (!string.IsNullOrWhiteSpace(volunteerUserId))
        {
            volunteerProfileId = await context.VolunteerProfiles
                .Where(profile => profile.UserId == volunteerUserId && profile.IsActive)
                .Select(profile => (int?)profile.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var task = await BaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(task => task.Id == taskId, cancellationToken);

        return task is null || !CanView(task, shelterId, volunteerProfileId, isAdmin)
            ? null
            : ToDetailsDto(task, shelterId, volunteerProfileId, isAdmin);
    }

    public async Task<VolunteerTaskStatsDto> GetTaskStatsAsync(
        int? shelterId = null,
        string? volunteerUserId = null,
        CancellationToken cancellationToken = default)
    {
        int? volunteerProfileId = null;
        if (!string.IsNullOrWhiteSpace(volunteerUserId))
        {
            volunteerProfileId = await context.VolunteerProfiles
                .Where(profile => profile.UserId == volunteerUserId)
                .Select(profile => (int?)profile.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var taskQuery = context.VolunteerTasks.AsNoTracking();
        if (shelterId.HasValue)
        {
            taskQuery = taskQuery.Where(task => task.ShelterId == shelterId.Value);
        }

        if (volunteerProfileId.HasValue)
        {
            taskQuery = taskQuery.Where(task => task.AssignedVolunteerProfileId == volunteerProfileId.Value);
        }

        var tasks = await taskQuery.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var weekStart = now.Date.AddDays(-(int)now.DayOfWeek);
        var activeVolunteersQuery = context.VolunteerProfiles.AsNoTracking().Where(profile => profile.IsActive);
        if (shelterId.HasValue)
        {
            activeVolunteersQuery = activeVolunteersQuery.Where(profile =>
                !profile.PreferredShelterId.HasValue || profile.PreferredShelterId == shelterId.Value);
        }

        return new VolunteerTaskStatsDto(
            OpenTasks: tasks.Count(task => task.Status == VolunteerTaskStatus.Open),
            AssignedTasks: tasks.Count(task => task.Status == VolunteerTaskStatus.Assigned),
            InProgressTasks: tasks.Count(task => task.Status == VolunteerTaskStatus.InProgress),
            CompletedThisWeek: tasks.Count(task => task.Status == VolunteerTaskStatus.Completed && task.CompletedAtUtc >= weekStart),
            OverdueTasks: tasks.Count(task => task.Status is VolunteerTaskStatus.Open or VolunteerTaskStatus.Assigned or VolunteerTaskStatus.InProgress &&
                (task.DueAtUtc ?? task.ScheduledEndUtc) < now),
            ActiveVolunteers: await activeVolunteersQuery.CountAsync(cancellationToken),
            TotalTasks: tasks.Count);
    }

    private IQueryable<VolunteerTask> BaseQuery()
    {
        return context.VolunteerTasks
            .Include(task => task.Shelter)
            .Include(task => task.Dog)
                .ThenInclude(dog => dog!.DogBreed)
            .Include(task => task.Dog)
                .ThenInclude(dog => dog!.SecondaryBreed)
            .Include(task => task.CreatedByUser)
            .Include(task => task.AssignedVolunteerProfile)
                .ThenInclude(profile => profile!.PreferredShelter)
            .Include(task => task.Activities)
                .ThenInclude(activity => activity.ActorUser)
            .AsSplitQuery();
    }

    private static IQueryable<VolunteerTask> ApplyTaskFilter(IQueryable<VolunteerTask> query, VolunteerTaskFilterDto? filter)
    {
        if (filter is null)
        {
            return query;
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(task => task.Status == filter.Status.Value);
        }

        if (filter.Category.HasValue)
        {
            query = query.Where(task => task.Category == filter.Category.Value);
        }

        if (filter.Priority.HasValue)
        {
            query = query.Where(task => task.Priority == filter.Priority.Value);
        }

        if (filter.ShelterId.HasValue)
        {
            query = query.Where(task => task.ShelterId == filter.ShelterId.Value);
        }

        if (filter.AssignedVolunteerProfileId.HasValue)
        {
            query = query.Where(task => task.AssignedVolunteerProfileId == filter.AssignedVolunteerProfileId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToUpper();
            query = query.Where(task =>
                task.Title.ToUpper().Contains(search) ||
                task.Description != null && task.Description.ToUpper().Contains(search) ||
                task.Shelter != null && task.Shelter.Name.ToUpper().Contains(search) ||
                task.Dog != null && task.Dog.Name.ToUpper().Contains(search) ||
                task.AssignedVolunteerProfile != null && task.AssignedVolunteerProfile.DisplayName.ToUpper().Contains(search));
        }

        return query;
    }

    private async Task<VolunteerProfile> LoadVolunteerProfileAsync(int volunteerProfileId, CancellationToken cancellationToken)
    {
        return await context.VolunteerProfiles
            .Include(profile => profile.PreferredShelter)
            .FirstOrDefaultAsync(profile => profile.Id == volunteerProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Volunteer profile was not found.");
    }

    private async Task<VolunteerProfile> LoadActiveVolunteerProfileForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await context.VolunteerProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Volunteer profile was not found.");

        if (!profile.IsActive)
        {
            throw new InvalidOperationException("Volunteer profile is inactive.");
        }

        return profile;
    }

    private async Task<(VolunteerTask Task, VolunteerProfile Profile)> LoadAssignedVolunteerTaskAsync(
        int taskId,
        string volunteerUserId,
        CancellationToken cancellationToken)
    {
        EnsureUserId(volunteerUserId);
        var profile = await LoadActiveVolunteerProfileForUserAsync(volunteerUserId, cancellationToken);
        var task = await LoadTaskForUpdateAsync(taskId, cancellationToken);
        if (task.AssignedVolunteerProfileId != profile.Id)
        {
            throw new InvalidOperationException("Only the assigned volunteer can update this task.");
        }

        return (task, profile);
    }

    private async Task<VolunteerTask> LoadTaskForUpdateAsync(int taskId, CancellationToken cancellationToken)
    {
        return await context.VolunteerTasks
            .Include(task => task.Activities)
            .FirstOrDefaultAsync(task => task.Id == taskId, cancellationToken)
            ?? throw new InvalidOperationException("Volunteer task was not found.");
    }

    private async Task<VolunteerTask> LoadRequiredTaskAsync(int taskId, CancellationToken cancellationToken)
    {
        return await BaseQuery()
            .FirstOrDefaultAsync(task => task.Id == taskId, cancellationToken)
            ?? throw new InvalidOperationException("Volunteer task was not found.");
    }

    private async Task AssignVolunteerCoreAsync(
        VolunteerTask task,
        int volunteerProfileId,
        string actorUserId,
        string? notes,
        CancellationToken cancellationToken,
        VolunteerTaskActivityType activityType = VolunteerTaskActivityType.Assigned)
    {
        var volunteer = await context.VolunteerProfiles
            .FirstOrDefaultAsync(profile => profile.Id == volunteerProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Volunteer profile was not found.");

        if (!volunteer.IsActive)
        {
            throw new InvalidOperationException("Volunteer profile is inactive.");
        }

        if (volunteer.PreferredShelterId.HasValue && volunteer.PreferredShelterId != task.ShelterId)
        {
            throw new InvalidOperationException("Volunteer is not assigned to this shelter.");
        }

        task.AssignedVolunteerProfileId = volunteer.Id;
        task.Status = VolunteerTaskStatus.Assigned;
        task.AssignedAtUtc = DateTime.UtcNow;
        task.StartedAtUtc = null;
        task.UpdatedAtUtc = task.AssignedAtUtc.Value;
        await EnsureNoVolunteerOverlapAsync(task, cancellationToken);
        AddActivity(task, activityType, actorUserId, notes ?? $"Assigned to {volunteer.DisplayName}.");
    }

    private async Task EnsureNoVolunteerOverlapAsync(VolunteerTask task, CancellationToken cancellationToken)
    {
        if (!task.AssignedVolunteerProfileId.HasValue ||
            !ActiveAssignmentStatuses.Contains(task.Status))
        {
            return;
        }

        var overlaps = await context.VolunteerTasks.AnyAsync(existing =>
            existing.Id != task.Id &&
            existing.AssignedVolunteerProfileId == task.AssignedVolunteerProfileId &&
            ActiveAssignmentStatuses.Contains(existing.Status) &&
            task.ScheduledStartUtc < existing.ScheduledEndUtc &&
            task.ScheduledEndUtc > existing.ScheduledStartUtc,
            cancellationToken);

        if (overlaps)
        {
            throw new InvalidOperationException("Volunteer already has an assigned task during this time.");
        }
    }

    private async Task EnsureUserExistsAsync(string userId, CancellationToken cancellationToken)
    {
        var exists = await context.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("User was not found.");
        }
    }

    private async Task EnsureShelterExistsAsync(int? shelterId, CancellationToken cancellationToken)
    {
        if (!shelterId.HasValue)
        {
            return;
        }

        var exists = await context.Shelters.AnyAsync(shelter => shelter.Id == shelterId.Value, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Shelter was not found.");
        }
    }

    private async Task EnsureDogBelongsToShelterAsync(int? dogId, int shelterId, CancellationToken cancellationToken)
    {
        if (!dogId.HasValue)
        {
            return;
        }

        var belongsToShelter = await context.Dogs.AnyAsync(dog => dog.Id == dogId.Value && dog.ShelterId == shelterId, cancellationToken);
        if (!belongsToShelter)
        {
            throw new InvalidOperationException("Dog was not found for this shelter.");
        }
    }

    private static void EnsureShelterOwnsTask(VolunteerTask task, int? shelterId)
    {
        if (!shelterId.HasValue || task.ShelterId != shelterId.Value)
        {
            throw new InvalidOperationException("Volunteer task was not found for your shelter.");
        }
    }

    private static void EnsureVolunteerCanAccessOpenTask(VolunteerTask task, VolunteerProfile profile)
    {
        if (profile.PreferredShelterId.HasValue && task.ShelterId != profile.PreferredShelterId.Value)
        {
            throw new InvalidOperationException("Volunteer task was not found for your shelter preferences.");
        }
    }

    private static bool CanView(VolunteerTask task, int? shelterId, int? volunteerProfileId, bool isAdmin)
    {
        return isAdmin ||
            shelterId.HasValue && task.ShelterId == shelterId.Value ||
            volunteerProfileId.HasValue && task.AssignedVolunteerProfileId == volunteerProfileId.Value ||
            volunteerProfileId.HasValue && task.Status == VolunteerTaskStatus.Open;
    }

    private static void ValidateSchedule(DateTime scheduledStartUtc, DateTime scheduledEndUtc)
    {
        if (scheduledStartUtc == default)
        {
            throw new InvalidOperationException("Scheduled start time is required.");
        }

        if (scheduledEndUtc == default)
        {
            throw new InvalidOperationException("Scheduled end time is required.");
        }

        if (ToUtc(scheduledEndUtc) <= ToUtc(scheduledStartUtc))
        {
            throw new InvalidOperationException("Scheduled end time must be after the start time.");
        }
    }

    private void AddActivity(VolunteerTask task, VolunteerTaskActivityType type, string? actorUserId, string? message)
    {
        task.Activities.Add(new VolunteerTaskActivity
        {
            ActivityType = type,
            ActorUserId = actorUserId,
            Message = NormalizeOptional(message, NotesMaxLength),
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private async Task NotifyVolunteerAssignedAsync(VolunteerTask task)
    {
        if (notificationService is null || task.AssignedVolunteerProfile is null)
        {
            return;
        }

        await notificationService.CreateNotificationAsync(
            task.AssignedVolunteerProfile.UserId,
            "Volunteer task assigned",
            $"You have been assigned to '{task.Title}' for {task.Shelter?.Name ?? "a shelter"}.",
            NotificationCategory.Volunteer,
            NotificationType.Info,
            "/volunteer/tasks",
            nameof(VolunteerTask),
            task.Id.ToString(),
            TimeSpan.FromMinutes(5));
    }

    private async Task NotifyVolunteerAsync(VolunteerTask task, string title, string message, NotificationType type)
    {
        if (notificationService is null || task.AssignedVolunteerProfile is null)
        {
            return;
        }

        await notificationService.CreateNotificationAsync(
            task.AssignedVolunteerProfile.UserId,
            title,
            message,
            NotificationCategory.Volunteer,
            type,
            "/volunteer/tasks",
            nameof(VolunteerTask),
            task.Id.ToString(),
            TimeSpan.FromMinutes(5));
    }

    private async Task NotifyShelterAsync(VolunteerTask task, string title, string message, NotificationType type)
    {
        if (notificationService is null || string.IsNullOrWhiteSpace(task.Shelter?.ApplicationUserId))
        {
            return;
        }

        await notificationService.CreateNotificationAsync(
            task.Shelter.ApplicationUserId,
            title,
            message,
            NotificationCategory.Volunteer,
            type,
            "/shelter/volunteer-tasks",
            nameof(VolunteerTask),
            task.Id.ToString(),
            TimeSpan.FromMinutes(5));
    }

    private async Task LogAsync(string action, string entityName, string? entityId, string description, string? userId)
    {
        if (auditLogService is null)
        {
            return;
        }

        await auditLogService.LogAsync(action, entityName, entityId, description, userId);
    }

    private static VolunteerProfileDto ToProfileDto(VolunteerProfile profile)
    {
        return new VolunteerProfileDto(
            profile.Id,
            profile.UserId,
            profile.DisplayName,
            profile.Email,
            profile.PhoneNumber,
            profile.PreferredShelterId,
            profile.PreferredShelter?.Name,
            profile.Skills,
            profile.AvailabilityNotes,
            profile.IsActive);
    }

    private static VolunteerTaskDto ToTaskDto(VolunteerTask task, int? shelterId, int? volunteerProfileId, bool isAdmin)
    {
        return new VolunteerTaskDto(
            task.Id,
            task.ShelterId,
            task.Shelter?.Name ?? "Unknown shelter",
            task.DogId,
            task.Dog?.Name,
            task.Title,
            task.Description,
            task.Category,
            task.Status,
            task.Priority,
            task.ScheduledStartUtc,
            task.ScheduledEndUtc,
            task.DueAtUtc,
            task.Location,
            task.RequiredSkills,
            task.AssignedVolunteerProfileId,
            task.AssignedVolunteerProfile?.DisplayName,
            task.AssignedAtUtc,
            task.StartedAtUtc,
            task.CompletedAtUtc,
            task.CancelledAtUtc,
            CanAssign(task, shelterId, isAdmin),
            CanAccept(task, volunteerProfileId),
            CanStart(task, volunteerProfileId),
            CanComplete(task, volunteerProfileId),
            CanCancel(task, shelterId, isAdmin));
    }

    private static VolunteerTaskDetailsDto ToDetailsDto(VolunteerTask task, int? shelterId, int? volunteerProfileId, bool isAdmin)
    {
        return new VolunteerTaskDetailsDto(
            task.Id,
            task.ShelterId,
            task.Shelter?.Name ?? "Unknown shelter",
            task.DogId,
            task.Dog?.Name,
            task.Dog is null ? null : DogBreedFormatter.Format(task.Dog),
            task.Title,
            task.Description,
            task.Category,
            task.Status,
            task.Priority,
            task.ScheduledStartUtc,
            task.ScheduledEndUtc,
            task.DueAtUtc,
            task.Location,
            task.RequiredSkills,
            task.ShelterNotes,
            task.VolunteerNotes,
            task.CompletionNotes,
            task.AssignedVolunteerProfileId,
            task.AssignedVolunteerProfile?.DisplayName,
            GetDisplayName(task.CreatedByUser),
            task.AssignedAtUtc,
            task.StartedAtUtc,
            task.CompletedAtUtc,
            task.CancelledAtUtc,
            task.CreatedAtUtc,
            task.UpdatedAtUtc,
            CanAssign(task, shelterId, isAdmin),
            CanAccept(task, volunteerProfileId),
            CanStart(task, volunteerProfileId),
            CanComplete(task, volunteerProfileId),
            CanCancel(task, shelterId, isAdmin),
            task.Activities
                .OrderByDescending(activity => activity.CreatedAtUtc)
                .Select(activity => new VolunteerTaskActivityDto(
                    activity.Id,
                    activity.ActivityType,
                    activity.Message,
                    GetDisplayName(activity.ActorUser),
                    activity.CreatedAtUtc))
                .ToList());
    }

    private static bool CanAssign(VolunteerTask task, int? shelterId, bool isAdmin)
    {
        return (isAdmin || shelterId == task.ShelterId) &&
            task.Status is VolunteerTaskStatus.Open or VolunteerTaskStatus.Assigned or VolunteerTaskStatus.InProgress;
    }

    private static bool CanAccept(VolunteerTask task, int? volunteerProfileId)
    {
        return volunteerProfileId.HasValue && task.Status == VolunteerTaskStatus.Open;
    }

    private static bool CanStart(VolunteerTask task, int? volunteerProfileId)
    {
        return volunteerProfileId.HasValue &&
            task.AssignedVolunteerProfileId == volunteerProfileId.Value &&
            task.Status == VolunteerTaskStatus.Assigned;
    }

    private static bool CanComplete(VolunteerTask task, int? volunteerProfileId)
    {
        return volunteerProfileId.HasValue &&
            task.AssignedVolunteerProfileId == volunteerProfileId.Value &&
            task.Status == VolunteerTaskStatus.InProgress;
    }

    private static bool CanCancel(VolunteerTask task, int? shelterId, bool isAdmin)
    {
        return (isAdmin || shelterId == task.ShelterId) &&
            task.Status is VolunteerTaskStatus.Open or VolunteerTaskStatus.Assigned or VolunteerTaskStatus.InProgress;
    }

    private static string GetDisplayName(ApplicationUser? user)
    {
        return string.IsNullOrWhiteSpace(user?.FullName)
            ? user?.Email ?? "Unknown user"
            : user.FullName;
    }

    private static string NormalizeRequired(string? value, string errorMessage, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? MergeNotes(string? existing, string label, string? note)
    {
        var normalizedNote = NormalizeOptional(note, NotesMaxLength);
        if (normalizedNote is null)
        {
            return existing;
        }

        var entry = $"{label}: {normalizedNote}";
        var merged = string.IsNullOrWhiteSpace(existing) ? entry : $"{existing}{Environment.NewLine}{entry}";
        return merged.Length <= NotesMaxLength ? merged : merged[..NotesMaxLength];
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime? ToUtcOrNull(DateTime? value)
    {
        return value.HasValue ? ToUtc(value.Value) : null;
    }

    private static void EnsureUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Current user could not be found.");
        }
    }
}
