namespace PawConnect.Services;

public sealed record DogSearchIndexRefreshResult(
    bool OpenAiEnabled,
    bool HasApiKey,
    string EmbeddingModel,
    int SearchableDogCount,
    int ExistingEmbeddingCount,
    int Created,
    int Updated,
    int SkippedUnchanged,
    int Removed,
    int Failed)
{
    public bool IsConfigured => OpenAiEnabled && HasApiKey;

    public int GeneratedRows => Created + Updated;

    public int SuccessfulRows => Created + Updated + SkippedUnchanged;

    public bool HasFailures => Failed > 0;
}
