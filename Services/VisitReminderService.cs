using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class VisitReminderService(
    ApplicationDbContext context,
    IEmailService emailService,
    IOptions<VisitReminderSettings> options,
    ILogger<VisitReminderService> logger,
    INotificationService? notificationService = null,
    IAuditLogService? auditLogService = null) : IVisitReminderService
{
    private readonly VisitReminderSettings settings = options.Value;

    public async Task<int> SendDueVisitRemindersAsync(CancellationToken cancellationToken = default)
    {
        var dueRequests = await GetDueVisitRemindersAsync(DateTime.Now, cancellationToken);
        logger.LogInformation("Visit reminder run found {ReminderCount} due reminders.", dueRequests.Count);

        var sentCount = 0;
        foreach (var request in dueRequests)
        {
            try
            {
                await SendVisitReminderAsync(request.Id, cancellationToken);
                sentCount++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Visit reminder failed for adoption request {RequestId}.", request.Id);
            }
        }

        return sentCount;
    }

    public Task<List<AdoptionRequest>> GetDueVisitRemindersAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var reminderTarget = DateTime.SpecifyKind(now, DateTimeKind.Unspecified)
            .AddHours(settings.GetSafeReminderHoursBeforeVisit());
        var reminderWindow = settings.GetReminderWindow();
        var from = reminderTarget.Subtract(reminderWindow);
        var to = reminderTarget.Add(reminderWindow);

        return context.AdoptionRequests
            .Include(request => request.Dog)
            .ThenInclude(dog => dog!.Shelter)
            .Include(request => request.Adopter)
            .Where(request =>
                request.Status == AdoptionRequestStatus.VisitConfirmed
                && request.VisitStatus == AdoptionVisitStatus.Confirmed
                && request.PreferredVisitDateTime.HasValue
                && request.PreferredVisitDateTime.Value >= from
                && request.PreferredVisitDateTime.Value <= to
                && request.VisitReminderSentAt == null
                && request.Adopter != null
                && request.Adopter.Email != null
                && request.Adopter.Email != string.Empty)
            .OrderBy(request => request.PreferredVisitDateTime)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task SendVisitReminderAsync(int adoptionRequestId, CancellationToken cancellationToken = default)
    {
        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .FirstOrDefaultAsync(r => r.Id == adoptionRequestId, cancellationToken);

        EnsureReminderCanBeSent(request);

        var adopterEmail = request!.Adopter!.Email!;
        var dogName = request.Dog?.Name ?? "the selected dog";
        var shelter = request.Dog?.Shelter;
        var shelterName = shelter?.Name ?? "the shelter";
        var visitTime = VisitSchedulingHelper.FormatVisitDateTime(request.PreferredVisitDateTime);
        var shelterAddress = FormatShelterAddress(shelter);
        var shelterEmail = string.IsNullOrWhiteSpace(shelter?.Email) ? "Not provided" : shelter.Email;
        var shelterPhone = string.IsNullOrWhiteSpace(shelter?.PhoneNumber) ? "Not provided" : shelter.PhoneNumber;
        var attachments = new List<EmailAttachment>
        {
            VisitSchedulingHelper.CreateCalendarInviteAttachment(request)
        };

        var body = $"""
            Hello {FormatGreetingName(request.Adopter)},

            This is a reminder that your adoption visit is scheduled for tomorrow.

            Dog: {dogName}
            Shelter: {shelterName}
            Visit time: {visitTime}
            Shelter address: {shelterAddress}
            Shelter email: {shelterEmail}
            Shelter phone: {shelterPhone}

            A calendar invitation is included in this email.
            If you cannot attend, please contact the shelter.
            """;

        var htmlBody = PawConnectEmailTemplate.BuildHtml(
            "Reminder: your PawConnect shelter visit is tomorrow",
            $"Hello {FormatGreetingName(request.Adopter)},",
            [
                "This is a reminder that your adoption visit is scheduled for tomorrow.",
                "A calendar invitation is included in this email.",
                "If you cannot attend, please contact the shelter."
            ],
            details:
            [
                new("Dog", dogName),
                new("Shelter", shelterName),
                new("Visit time", visitTime),
                new("Shelter address", shelterAddress),
                new("Shelter email", shelterEmail),
                new("Shelter phone", shelterPhone)
            ],
            note: "PawConnect does not use Google Calendar API. The attached calendar file can be imported by common calendar apps.");

        cancellationToken.ThrowIfCancellationRequested();
        await emailService.SendEmailAsync(
            adopterEmail,
            "Reminder: your PawConnect shelter visit is tomorrow",
            body,
            attachments,
            htmlBody);

        request.VisitReminderSentAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        if (notificationService is not null)
        {
            await notificationService.CreateNotificationAsync(
                request.AdopterId,
                "Visit reminder",
                $"Your visit for {dogName} is scheduled for tomorrow.",
                NotificationCategory.Adoption,
                NotificationType.Info,
                "/my-adoption-requests",
                "AdoptionRequest",
                request.Id.ToString());
        }

        if (auditLogService is not null)
        {
            await auditLogService.LogSystemAsync(
                AuditActions.VisitReminderSent,
                "AdoptionRequest",
                request.Id.ToString(),
                $"Visit reminder sent for adoption request {request.Id}.",
                additionalData: $"DogId={request.DogId};VisitTime={request.PreferredVisitDateTime:O}");
        }

        logger.LogInformation("Visit reminder sent for adoption request {RequestId}.", request.Id);
    }

    private static void EnsureReminderCanBeSent(AdoptionRequest? request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Adoption request was not found.");
        }

        if (request.Status != AdoptionRequestStatus.VisitConfirmed || request.VisitStatus != AdoptionVisitStatus.Confirmed)
        {
            throw new InvalidOperationException("Only confirmed visits can receive reminder emails.");
        }

        if (!request.PreferredVisitDateTime.HasValue)
        {
            throw new InvalidOperationException("Visit time is not available.");
        }

        if (request.VisitReminderSentAt.HasValue)
        {
            throw new InvalidOperationException("Visit reminder was already sent.");
        }

        if (string.IsNullOrWhiteSpace(request.Adopter?.Email))
        {
            throw new InvalidOperationException("Adopter email is not available.");
        }
    }

    private static string FormatShelterAddress(Shelter? shelter)
    {
        var address = string.Join(", ", new[] { shelter?.Address, shelter?.City }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(address) ? "Not provided" : address;
    }

    private static string FormatGreetingName(ApplicationUser adopter)
    {
        return string.IsNullOrWhiteSpace(adopter.FullName)
            ? adopter.Email ?? "there"
            : adopter.FullName.Trim();
    }
}
