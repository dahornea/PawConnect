using PawConnect.Entities;

namespace PawConnect.Services;

public interface INotificationPreferenceService
{
    IReadOnlyList<NotificationTypeDescriptionDto> GetNotificationTypes();

    Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task SavePreferencesAsync(
        string userId,
        IReadOnlyList<NotificationPreferenceUpdateDto> updates,
        CancellationToken cancellationToken = default);

    Task<bool> IsChannelEnabledAsync(
        string userId,
        NotificationEventType notificationType,
        NotificationChannel channel,
        CancellationToken cancellationToken = default);
}
