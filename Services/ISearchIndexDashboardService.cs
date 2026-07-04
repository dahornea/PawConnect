namespace PawConnect.Services;

public interface ISearchIndexDashboardService
{
    Task<SearchIndexDashboardSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DogEmbeddingStatusDto>> GetDogEmbeddingStatusesAsync(
        EmbeddingLifecycleFilterDto? filter = null,
        CancellationToken cancellationToken = default);

    Task<EmbeddingRebuildResultDto> RebuildDogEmbeddingAsync(
        int dogId,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingRebuildResultDto> RebuildMissingEmbeddingsAsync(
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingRebuildResultDto> RebuildStaleEmbeddingsAsync(
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingRebuildResultDto> RebuildAllPublicSafeEmbeddingsAsync(
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<SearchDocumentPreviewDto> GetSearchDocumentPreviewAsync(
        int dogId,
        string adminUserId,
        CancellationToken cancellationToken = default);
}
