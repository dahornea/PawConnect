using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Shelter;

public partial class ShelterVolunteerTasks
{
    [Inject] private IVolunteerTaskService VolunteerTaskService { get; set; } = default!;
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<VolunteerTaskDto> _tasks = [];
    private readonly List<VolunteerProfileDto> _volunteers = [];
    private List<Dog> _dogs = [];
    private VolunteerTaskStatsDto? _stats;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;
    private int? _shelterId;
    private string? _currentUserId;

    private string? _title;
    private string? _description;
    private VolunteerTaskCategory _category = VolunteerTaskCategory.DogWalking;
    private VolunteerTaskPriority _priority = VolunteerTaskPriority.Normal;
    private DateTime? _taskDate = DateTime.Today;
    private string _startTime = "09:00";
    private string _endTime = "10:00";
    private int? _dogId;
    private int? _assignedVolunteerId;
    private string? _location;
    private string? _requiredSkills;

    private IReadOnlyList<VolunteerTaskDto> TodayTasks =>
        _tasks
            .Where(task => task.ScheduledStartUtc.ToLocalTime().Date == DateTime.Today)
            .ToList();

    private IReadOnlyList<VolunteerTaskDto> UpcomingTasks =>
        _tasks
            .Where(task => task.ScheduledStartUtc.ToLocalTime().Date > DateTime.Today &&
                task.Status is not VolunteerTaskStatus.Completed and not VolunteerTaskStatus.Cancelled)
            .ToList();

    private IReadOnlyList<VolunteerTaskDto> OpenTasks =>
        _tasks.Where(task => task.Status == VolunteerTaskStatus.Open).ToList();

    private IReadOnlyList<VolunteerTaskDto> AssignedTasks =>
        _tasks.Where(task => task.Status is VolunteerTaskStatus.Assigned or VolunteerTaskStatus.InProgress).ToList();

    private IReadOnlyList<VolunteerTaskDto> CompletedTasks =>
        _tasks.Where(task => task.Status == VolunteerTaskStatus.Completed).ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(authenticationState.User);
            _currentUserId = user?.Id ?? authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                _error = "Current user could not be resolved.";
                return;
            }

            var shelter = await ShelterService.GetShelterForUserAsync(_currentUserId);
            if (shelter is null)
            {
                _error = "No shelter profile is linked to this account.";
                return;
            }

            _shelterId = shelter.Id;
            _dogs = (await DogService.GetDogsForShelterAsync(shelter.Id))
                .OrderBy(dog => dog.Name)
                .ToList();
            await RefreshTasksAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshTasksAsync()
    {
        if (_shelterId is null)
        {
            return;
        }

        var volunteers = await VolunteerTaskService.GetVolunteersForShelterAsync(_shelterId.Value);
        _volunteers.Clear();
        _volunteers.AddRange(volunteers);

        var tasks = await VolunteerTaskService.GetShelterTasksAsync(_shelterId.Value);
        _tasks.Clear();
        _tasks.AddRange(tasks);
        _stats = await VolunteerTaskService.GetTaskStatsAsync(_shelterId.Value);
    }

    private async Task CreateTaskAsync()
    {
        if (_shelterId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Shelter account could not be resolved.", Severity.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(_title))
        {
            Snackbar.Add("Task title is required.", Severity.Warning);
            return;
        }

        if (!TryBuildSchedule(out var startUtc, out var endUtc))
        {
            Snackbar.Add("Enter a valid date, start time, and end time.", Severity.Warning);
            return;
        }

        _isSaving = true;
        try
        {
            await VolunteerTaskService.CreateTaskAsync(
                _shelterId.Value,
                _currentUserId,
                new VolunteerTaskCreateRequest(
                    _title,
                    _description,
                    _category,
                    _priority,
                    startUtc,
                    endUtc,
                    endUtc,
                    _dogId,
                    _assignedVolunteerId,
                    _location,
                    _requiredSkills,
                    _description));

            Snackbar.Add("Volunteer task created.", Severity.Success);
            ResetForm();
            await RefreshTasksAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task AssignAsync(int taskId, int? volunteerProfileId)
    {
        if (_shelterId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Shelter account could not be resolved.", Severity.Error);
            return;
        }

        _isSaving = true;
        try
        {
            await VolunteerTaskService.AssignTaskAsync(
                taskId,
                _shelterId.Value,
                _currentUserId,
                new VolunteerTaskAssignRequest(volunteerProfileId, volunteerProfileId is null ? "Assignment removed." : "Assigned from shelter task board."));

            Snackbar.Add(volunteerProfileId is null ? "Task unassigned." : "Task assigned.", Severity.Success);
            await RefreshTasksAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ViewTaskAsync(int taskId)
    {
        if (_shelterId is null)
        {
            return;
        }

        try
        {
            var task = await VolunteerTaskService.GetTaskDetailsAsync(taskId, _shelterId.Value);
            if (task is null)
            {
                Snackbar.Add("Volunteer task was not found.", Severity.Warning);
                return;
            }

            var parameters = new DialogParameters<VolunteerTaskDetailsDialog>
            {
                { dialog => dialog.Task, task }
            };
            var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
            await DialogService.ShowAsync<VolunteerTaskDetailsDialog>("Volunteer task details", parameters, options);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task AddCommentAsync(int taskId)
    {
        var notes = await AskForNotesAsync("Add task note", "Add a short operational note for this task.", "Save note", Color.Info);
        if (notes is null)
        {
            return;
        }

        await RunTaskActionAsync(
            () => VolunteerTaskService.AddTaskCommentAsync(taskId, _currentUserId!, new VolunteerTaskActionRequest(notes), _shelterId),
            "Task note saved.");
    }

    private async Task CancelAsync(int taskId)
    {
        var notes = await AskForNotesAsync("Cancel volunteer task", "Add optional cancellation notes.", "Cancel task", Color.Default);
        if (notes is null)
        {
            return;
        }

        await RunTaskActionAsync(
            () => VolunteerTaskService.CancelTaskAsync(taskId, _shelterId, _currentUserId!, request: new VolunteerTaskActionRequest(notes)),
            "Volunteer task cancelled.");
    }

    private async Task RunTaskActionAsync(Func<Task> action, string successMessage)
    {
        if (_shelterId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Shelter account could not be resolved.", Severity.Error);
            return;
        }

        _isSaving = true;
        try
        {
            await action();
            Snackbar.Add(successMessage, Severity.Success);
            await RefreshTasksAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<string?> AskForNotesAsync(string title, string message, string confirmText, Color confirmColor)
    {
        var parameters = new DialogParameters<DogTransferDecisionDialog>
        {
            { dialog => dialog.Message, message },
            { dialog => dialog.ConfirmText, confirmText },
            { dialog => dialog.ConfirmColor, confirmColor }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<DogTransferDecisionDialog>(title, parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: string notes } ? notes : null;
    }

    private bool TryBuildSchedule(out DateTime startUtc, out DateTime endUtc)
    {
        startUtc = default;
        endUtc = default;

        if (_taskDate is null ||
            !TimeSpan.TryParse(_startTime, out var startTime) ||
            !TimeSpan.TryParse(_endTime, out var endTime))
        {
            return false;
        }

        var localStart = DateTime.SpecifyKind(_taskDate.Value.Date.Add(startTime), DateTimeKind.Local);
        var localEnd = DateTime.SpecifyKind(_taskDate.Value.Date.Add(endTime), DateTimeKind.Local);
        startUtc = localStart.ToUniversalTime();
        endUtc = localEnd.ToUniversalTime();
        return endUtc > startUtc;
    }

    private void ResetForm()
    {
        _title = null;
        _description = null;
        _category = VolunteerTaskCategory.DogWalking;
        _priority = VolunteerTaskPriority.Normal;
        _taskDate = DateTime.Today;
        _startTime = "09:00";
        _endTime = "10:00";
        _dogId = null;
        _assignedVolunteerId = null;
        _location = null;
        _requiredSkills = null;
    }

    private static string FormatDateTime(DateTime value) => value.ToLocalTime().ToString("dd MMM yyyy HH:mm");

    private static string FormatTime(DateTime value) => value.ToLocalTime().ToString("HH:mm");

    private static string FormatCategory(VolunteerTaskCategory category) => category switch
    {
        VolunteerTaskCategory.DogWalking => "Dog walking",
        VolunteerTaskCategory.MedicalVisitSupport => "Medical visit support",
        VolunteerTaskCategory.AdoptionEventSupport => "Adoption event support",
        _ => category.ToString()
    };

    private static string FormatStatus(VolunteerTaskStatus status) => status switch
    {
        VolunteerTaskStatus.InProgress => "In progress",
        _ => status.ToString()
    };

    private static Color GetStatusColor(VolunteerTaskStatus status) => status switch
    {
        VolunteerTaskStatus.Open => Color.Info,
        VolunteerTaskStatus.Assigned => Color.Warning,
        VolunteerTaskStatus.InProgress => Color.Primary,
        VolunteerTaskStatus.Completed => Color.Success,
        VolunteerTaskStatus.Cancelled => Color.Default,
        _ => Color.Default
    };

    private static Color GetPriorityColor(VolunteerTaskPriority priority) => priority switch
    {
        VolunteerTaskPriority.Low => Color.Default,
        VolunteerTaskPriority.Normal => Color.Info,
        VolunteerTaskPriority.High => Color.Warning,
        VolunteerTaskPriority.Urgent => Color.Error,
        _ => Color.Default
    };
}
