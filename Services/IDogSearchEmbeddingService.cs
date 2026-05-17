using PawConnect.Entities;

namespace PawConnect.Services;

public interface IDogSearchEmbeddingService
{
    Task<bool> RefreshDogEmbeddingAsync(int dogId, CancellationToken cancellationToken = default);

    Task<int> RefreshMissingDogEmbeddingsAsync(CancellationToken cancellationToken = default);

    Task<int> RefreshAllDogEmbeddingsAsync(CancellationToken cancellationToken = default);

    Task<DogSearchIndexRefreshResult> RebuildDogSearchIndexAsync(CancellationToken cancellationToken = default);

    Task<List<DogSearchEmbedding>> GetSearchableDogEmbeddingsAsync(CancellationToken cancellationToken = default);
}
