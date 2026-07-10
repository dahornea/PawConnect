using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services.Intelligence;

public sealed class VolunteerTaskSignalProvider(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IOptions<IntelligenceHubOptions> options) : IIntelligenceSignalProvider
{
    private readonly IntelligenceHubOptions settings = options.Value;

    public string ProviderKey => "VolunteerTasks";

    public async Task<IReadOnlyCollection<IntelligenceSignal>> CollectSignalsAsync(IntelligenceContext context, CancellationToken cancellationToken)
    {
        if (context.AudienceType == IntelligenceAudienceType.Adopter)
        {
            return [];
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.VolunteerTasks.AsNoTracking().Include(task => task.Shelter).AsQueryable();
        if (context.AudienceType == IntelligenceAudienceType.Shelter)
        {
            query = query.Where(task => task.ShelterId == context.ShelterId);
        }

        var tasks = await query
            .Where(task => task.Status != VolunteerTaskStatus.Completed && task.Status != VolunteerTaskStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var route = context.AudienceType == IntelligenceAudienceType.Shelter ? "/shelter/volunteer-tasks" : "/admin/volunteer-tasks";
        var signals = new List<IntelligenceSignal>();

        foreach (var task in tasks)
        {
            var deadline = task.DueAtUtc ?? task.ScheduledEndUtc;
            var overdueHours = (context.UtcNow - deadline).TotalHours;
            if (overdueHours >= settings.VolunteerTaskOverdueWarningHours)
            {
                signals.Add(new IntelligenceSignal(
                    $"VolunteerTaskOverdue:{task.Id}", IntelligenceCategory.Volunteer, "VolunteerTasks", "VolunteerTask",
                    task.Id.ToString(), task.Title, null, task.ShelterId,
                    $"Volunteer task is overdue: {task.Title}",
                    $"The task deadline passed {Math.Floor(overdueHours)} hours ago and its status is {task.Status}.",
                    "Overdue operational work can affect dog care, appointments, or shelter routines.",
                    "the task is completed, cancelled, or rescheduled with a future deadline",
                    $"Overdue by at least {settings.VolunteerTaskOverdueWarningHours} hours",
                    [$"Deadline: {deadline:dd MMM yyyy HH:mm}", $"Status: {task.Status}", $"Priority: {task.Priority}", task.AssignedVolunteerProfileId.HasValue ? "Volunteer assigned" : "No volunteer assigned"],
                    [new("Overdue time", overdueHours >= 48 ? 40 : 28, $"Overdue by {Math.Floor(overdueHours)} hours."), new("Task priority", task.Priority is VolunteerTaskPriority.Urgent ? 30 : task.Priority is VolunteerTaskPriority.High ? 22 : 12, $"Priority is {task.Priority}."), new("Assignment risk", task.AssignedVolunteerProfileId.HasValue ? 5 : 18, task.AssignedVolunteerProfileId.HasValue ? "A volunteer is assigned." : "No volunteer is assigned.")],
                    [new("open-task", "Open volunteer task", "Review, assign, or reschedule the task.", "Navigate", route, context.AudienceType.ToString(), "VolunteerTask", task.Id.ToString(), true)],
                    context.UtcNow));
                continue;
            }

            var startsWithinHours = (task.ScheduledStartUtc - context.UtcNow).TotalHours;
            if (task.Status == VolunteerTaskStatus.Open && !task.AssignedVolunteerProfileId.HasValue && startsWithinHours is >= 0 and <= 24 && task.Priority is VolunteerTaskPriority.High or VolunteerTaskPriority.Urgent)
            {
                signals.Add(new IntelligenceSignal(
                    $"UrgentTaskUnassigned:{task.Id}", IntelligenceCategory.Volunteer, "VolunteerTasks", "VolunteerTask",
                    task.Id.ToString(), task.Title, null, task.ShelterId,
                    $"Urgent task needs a volunteer: {task.Title}",
                    $"The task starts in {Math.Ceiling(startsWithinHours)} hours and has no assigned volunteer.",
                    "Finding an assignee before the scheduled start reduces last-minute operational disruption.",
                    "a volunteer is assigned, the task is rescheduled, completed, or cancelled",
                    "High or urgent task starts within 24 hours without an assignee",
                    [$"Starts: {task.ScheduledStartUtc:dd MMM yyyy HH:mm}", $"Priority: {task.Priority}", "Assigned volunteer: none"],
                    [new("Time sensitivity", 34, $"Starts in {Math.Ceiling(startsWithinHours)} hours."), new("Priority", task.Priority == VolunteerTaskPriority.Urgent ? 30 : 22, $"Priority is {task.Priority}."), new("Assignment gap", 20, "No volunteer is assigned.")],
                    [new("assign-task", "Assign volunteer", "Open the task workspace and assign an available volunteer.", "Navigate", route, context.AudienceType.ToString(), "VolunteerTask", task.Id.ToString(), true)],
                    context.UtcNow));
            }
        }

        return signals;
    }
}

