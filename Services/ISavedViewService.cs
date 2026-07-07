using PawConnect.Entities;

namespace PawConnect.Services;

public interface ISavedViewService
{
    Task<IReadOnlyList<SavedViewDto>> GetViewsForPageAsync(
        string userId,
        IEnumerable<string> userRoles,
        string pageKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedViewDto>> GetPinnedViewsAsync(
        string userId,
        IEnumerable<string> userRoles,
        CancellationToken cancellationToken = default);

    Task<SavedViewDto?> GetDefaultViewAsync(
        string userId,
        IEnumerable<string> userRoles,
        string pageKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedViewDto>> GetSystemViewsForPageAsync(
        IEnumerable<string> userRoles,
        string pageKey,
        CancellationToken cancellationToken = default);

    Task<SavedViewDto> CreateViewAsync(
        string userId,
        IEnumerable<string> userRoles,
        SavedViewCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<SavedViewDto> UpdateViewAsync(
        int savedViewId,
        string userId,
        IEnumerable<string> userRoles,
        SavedViewUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<SavedViewDto> RenameViewAsync(
        int savedViewId,
        string userId,
        SavedViewRenameRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteViewAsync(
        int savedViewId,
        string userId,
        CancellationToken cancellationToken = default);

    Task SetDefaultViewAsync(
        int savedViewId,
        string userId,
        IEnumerable<string> userRoles,
        CancellationToken cancellationToken = default);

    Task PinViewAsync(
        int savedViewId,
        string userId,
        CancellationToken cancellationToken = default);

    Task UnpinViewAsync(
        int savedViewId,
        string userId,
        CancellationToken cancellationToken = default);

    Task MarkViewUsedAsync(
        int savedViewId,
        string userId,
        IEnumerable<string> userRoles,
        CancellationToken cancellationToken = default);
}
