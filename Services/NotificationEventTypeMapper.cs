using PawConnect.Entities;

namespace PawConnect.Services;

public static class NotificationEventTypeMapper
{
    public static NotificationEventType FromNotification(
        NotificationCategory category,
        string? relatedEntityName,
        string? title = null)
    {
        if (Contains(title, "visit reminder"))
        {
            return NotificationEventType.VisitReminders;
        }

        if (Contains(title, "message") || IsRelatedEntity(relatedEntityName, "Message"))
        {
            return NotificationEventType.Messages;
        }

        if (IsRelatedEntity(relatedEntityName, "LostFoundPost"))
        {
            return NotificationEventType.LostFoundUpdates;
        }

        if (IsRelatedEntity(relatedEntityName, "DogTransferRequest") || Contains(title, "transfer"))
        {
            return NotificationEventType.DogTransferUpdates;
        }

        return category switch
        {
            NotificationCategory.Adoption => NotificationEventType.AdoptionRequestUpdates,
            NotificationCategory.Resource => NotificationEventType.ResourceAlerts,
            NotificationCategory.Report => NotificationEventType.ReportUpdates,
            NotificationCategory.ShelterApplication => NotificationEventType.ShelterApplicationUpdates,
            NotificationCategory.Transfer => NotificationEventType.DogTransferUpdates,
            _ => NotificationEventType.SystemAnnouncements
        };
    }

    public static NotificationEventType FromEmailSubject(string? subject)
    {
        if (Contains(subject, "visit") || Contains(subject, "calendar"))
        {
            return NotificationEventType.VisitReminders;
        }

        if (Contains(subject, "message"))
        {
            return NotificationEventType.Messages;
        }

        if (Contains(subject, "resource") || Contains(subject, "stock"))
        {
            return NotificationEventType.ResourceAlerts;
        }

        if (Contains(subject, "report"))
        {
            return NotificationEventType.ReportUpdates;
        }

        if (Contains(subject, "shelter application"))
        {
            return NotificationEventType.ShelterApplicationUpdates;
        }

        if (Contains(subject, "lost") || Contains(subject, "found"))
        {
            return NotificationEventType.LostFoundUpdates;
        }

        if (Contains(subject, "transfer"))
        {
            return NotificationEventType.DogTransferUpdates;
        }

        if (Contains(subject, "adoption") || Contains(subject, "request"))
        {
            return NotificationEventType.AdoptionRequestUpdates;
        }

        return NotificationEventType.SystemAnnouncements;
    }

    public static bool IsAccountSecurityEmail(string? subject)
    {
        return Contains(subject, "confirm your pawconnect account")
            || Contains(subject, "confirm your email")
            || Contains(subject, "reset your pawconnect password")
            || Contains(subject, "reset your password");
    }

    private static bool IsRelatedEntity(string? relatedEntityName, string value)
    {
        return string.Equals(relatedEntityName, value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string? value, string fragment)
    {
        return value?.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
