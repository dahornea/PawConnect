using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class NotificationPreferenceService(IDbContextFactory<ApplicationDbContext> contextFactory) : INotificationPreferenceService
{
    private static readonly IReadOnlyList<NotificationTypeDescriptionDto> TypeDescriptions =
    [
        new(NotificationEventType.AdoptionRequestUpdates, "Adoption request updates", "Status changes, visit confirmations, and adoption workflow updates.", true, true),
        new(NotificationEventType.VisitReminders, "Visit reminders", "Reminders for scheduled shelter visits.", true, true),
        new(NotificationEventType.Messages, "Adoption messages", "New messages in adoption-request conversations.", true, false),
        new(NotificationEventType.ResourceAlerts, "Resource alerts", "Low-stock shelter resource warnings.", true, true),
        new(NotificationEventType.ReportUpdates, "Report updates", "Generated CSV/PDF/report notifications.", true, true),
        new(NotificationEventType.ShelterApplicationUpdates, "Shelter application updates", "Review results for shelter registration requests.", true, true),
        new(NotificationEventType.LostFoundUpdates, "Lost & Found updates", "Moderation or status updates for lost and found posts.", true, false),
        new(NotificationEventType.DogTransferUpdates, "Dog transfer updates", "Requests, approvals, and completions for shelter dog transfers.", true, true),
        new(NotificationEventType.VolunteerTaskUpdates, "Volunteer task updates", "Assignments, schedule changes, and completion updates for volunteer tasks.", true, true),
        new(NotificationEventType.SystemAnnouncements, "System announcements", "General PawConnect account and platform updates.", true, false)
    ];

    public IReadOnlyList<NotificationTypeDescriptionDto> GetNotificationTypes()
    {
        return TypeDescriptions;
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var savedPreferences = await context.NotificationPreferences
            .Where(preference => preference.UserId == userId)
            .AsNoTracking()
            .ToDictionaryAsync(preference => preference.NotificationType, cancellationToken);

        return TypeDescriptions
            .Select(description =>
            {
                var hasSavedPreference = savedPreferences.TryGetValue(description.NotificationType, out var saved);
                return new NotificationPreferenceDto(
                    description.NotificationType,
                    description.DisplayName,
                    description.Description,
                    hasSavedPreference ? saved!.InAppEnabled : description.DefaultInAppEnabled,
                    hasSavedPreference ? saved!.EmailEnabled : description.DefaultEmailEnabled,
                    description.DefaultInAppEnabled,
                    description.DefaultEmailEnabled);
            })
            .ToList();
    }

    public async Task SavePreferencesAsync(
        string userId,
        IReadOnlyList<NotificationPreferenceUpdateDto> updates,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId);

        var validTypes = TypeDescriptions.Select(description => description.NotificationType).ToHashSet();
        var normalizedUpdates = updates
            .Where(update => validTypes.Contains(update.NotificationType))
            .GroupBy(update => update.NotificationType)
            .Select(group => group.Last())
            .ToList();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.NotificationPreferences
            .Where(preference => preference.UserId == userId)
            .ToDictionaryAsync(preference => preference.NotificationType, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var update in normalizedUpdates)
        {
            if (existing.TryGetValue(update.NotificationType, out var preference))
            {
                preference.InAppEnabled = update.InAppEnabled;
                preference.EmailEnabled = update.EmailEnabled;
                preference.UpdatedAt = now;
                continue;
            }

            context.NotificationPreferences.Add(new NotificationPreference
            {
                UserId = userId,
                NotificationType = update.NotificationType,
                InAppEnabled = update.InAppEnabled,
                EmailEnabled = update.EmailEnabled,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsChannelEnabledAsync(
        string userId,
        NotificationEventType notificationType,
        NotificationChannel channel,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId);

        var defaults = TypeDescriptions.FirstOrDefault(description => description.NotificationType == notificationType);
        if (defaults is null)
        {
            return channel == NotificationChannel.InApp;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var saved = await context.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                preference => preference.UserId == userId && preference.NotificationType == notificationType,
                cancellationToken);

        return channel switch
        {
            NotificationChannel.InApp => saved?.InAppEnabled ?? defaults.DefaultInAppEnabled,
            NotificationChannel.Email => saved?.EmailEnabled ?? defaults.DefaultEmailEnabled,
            _ => false
        };
    }

    private static void EnsureUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Current user could not be found.");
        }
    }
}


