using PawConnect.Entities;

namespace PawConnect.Services;

public interface IVisitReminderService
{
    Task<int> SendDueVisitRemindersAsync(CancellationToken cancellationToken = default);

    Task<List<AdoptionRequest>> GetDueVisitRemindersAsync(DateTime now, CancellationToken cancellationToken = default);

    Task SendVisitReminderAsync(int adoptionRequestId, CancellationToken cancellationToken = default);
}
