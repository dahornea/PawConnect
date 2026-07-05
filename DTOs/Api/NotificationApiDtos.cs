using PawConnect.Entities;

namespace PawConnect.DTOs.Api;

public sealed record NotificationPreferenceApiDto(
    NotificationEventType NotificationType,
    string DisplayName,
    string Description,
    bool InAppEnabled,
    bool EmailEnabled,
    bool DefaultInAppEnabled,
    bool DefaultEmailEnabled);

public sealed record UpdateNotificationPreferenceApiRequest(
    NotificationEventType NotificationType,
    bool InAppEnabled,
    bool EmailEnabled);
