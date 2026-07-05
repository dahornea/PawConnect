using PawConnect.Entities;

namespace PawConnect.Services;

public sealed record NotificationTypeDescriptionDto(
    NotificationEventType NotificationType,
    string DisplayName,
    string Description,
    bool DefaultInAppEnabled,
    bool DefaultEmailEnabled);

public sealed record NotificationPreferenceDto(
    NotificationEventType NotificationType,
    string DisplayName,
    string Description,
    bool InAppEnabled,
    bool EmailEnabled,
    bool DefaultInAppEnabled,
    bool DefaultEmailEnabled);

public sealed record NotificationPreferenceUpdateDto(
    NotificationEventType NotificationType,
    bool InAppEnabled,
    bool EmailEnabled);
