using PawConnect.Entities;

namespace PawConnect.Services;

public enum EmbeddingLifecycleStatus
{
    UpToDate,
    Missing,
    Stale,
    Failed,
    Skipped,
    OpenAiDisabled,
    NotPublicSafe
}

public sealed record EmbeddingLifecycleFilterDto(
    EmbeddingLifecycleStatus? Status = null,
    string? SearchTerm = null,
    bool PublicSafeOnly = false);

public sealed record SearchIndexDashboardSummaryDto(
    int TotalDogs,
    int PublicSafeDogs,
    int UpToDateCount,
    int MissingCount,
    int StaleCount,
    int FailedCount,
    int NotPublicSafeCount,
    string EmbeddingModel,
    bool OpenAiEnabled,
    bool HasApiKey,
    bool KeywordFallbackAvailable,
    DateTime? LastFullRebuildAt)
{
    public bool EmbeddingsConfigured => OpenAiEnabled && HasApiKey;
}

public sealed record DogEmbeddingStatusDto(
    int DogId,
    string DogName,
    DogStatus DogStatus,
    string? ShelterName,
    EmbeddingLifecycleStatus LifecycleStatus,
    DateTime? LastEmbeddedAt,
    string? EmbeddingModel,
    bool ContentHashMatches,
    bool EmbeddingModelMatches,
    int DocumentLength,
    string CurrentContentHash,
    string? StoredContentHash,
    bool IsPublicSafe,
    string? StatusReason);

public sealed record EmbeddingRebuildResultDto(
    int Requested,
    int Succeeded,
    int Failed,
    int Skipped,
    string Message,
    bool OpenAiConfigured);

public sealed record SearchDocumentPreviewDto(
    int DogId,
    string DogName,
    string Content,
    string ContentHash,
    int DocumentLength);
