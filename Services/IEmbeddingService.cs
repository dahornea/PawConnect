namespace PawConnect.Services;

public interface IEmbeddingService
{
    Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b);
}
