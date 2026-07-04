using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class SearchIndexDashboardService(
    ApplicationDbContext context,
    IDogSearchDocumentService documentService,
    IDogSearchEmbeddingService embeddingService,
    IOptions<OpenAiSettings> options,
    UserManager<ApplicationUser> userManager,
    ILogger<SearchIndexDashboardService> logger) : ISearchIndexDashboardService
{
    public async Task<SearchIndexDashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await BuildStatusesAsync(cancellationToken);
        var settings = options.Value;

        return new SearchIndexDashboardSummaryDto(
            TotalDogs: statuses.Count,
            PublicSafeDogs: statuses.Count(status => status.IsPublicSafe),
            UpToDateCount: statuses.Count(status => status.LifecycleStatus == EmbeddingLifecycleStatus.UpToDate),
            MissingCount: statuses.Count(status => status.LifecycleStatus is EmbeddingLifecycleStatus.Missing or EmbeddingLifecycleStatus.OpenAiDisabled),
            StaleCount: statuses.Count(status => status.LifecycleStatus == EmbeddingLifecycleStatus.Stale),
            FailedCount: statuses.Count(status => status.LifecycleStatus == EmbeddingLifecycleStatus.Failed),
            NotPublicSafeCount: statuses.Count(status => status.LifecycleStatus == EmbeddingLifecycleStatus.NotPublicSafe),
            EmbeddingModel: settings.GetSafeEmbeddingModel(),
            OpenAiEnabled: settings.Enabled,
            HasApiKey: settings.HasApiKey,
            KeywordFallbackAvailable: true,
            LastFullRebuildAt: statuses
                .Where(status => status.IsPublicSafe)
                .Select(status => status.LastEmbeddedAt)
                .Where(value => value.HasValue)
                .DefaultIfEmpty()
                .Min());
    }

    public async Task<IReadOnlyList<DogEmbeddingStatusDto>> GetDogEmbeddingStatusesAsync(
        EmbeddingLifecycleFilterDto? filter = null,
        CancellationToken cancellationToken = default)
    {
        var statuses = await BuildStatusesAsync(cancellationToken);

        if (filter?.PublicSafeOnly == true)
        {
            statuses = statuses.Where(status => status.IsPublicSafe).ToList();
        }

        if (filter?.Status is { } statusFilter)
        {
            statuses = statuses
                .Where(status => status.LifecycleStatus == statusFilter)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(filter?.SearchTerm))
        {
            var search = filter.SearchTerm.Trim();
            statuses = statuses
                .Where(status =>
                    status.DogName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(status.ShelterName) &&
                     status.ShelterName.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return statuses
            .OrderBy(status => GetStatusSort(status.LifecycleStatus))
            .ThenBy(status => status.DogName)
            .ToList();
    }

    public async Task<EmbeddingRebuildResultDto> RebuildDogEmbeddingAsync(
        int dogId,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId);

        var settings = options.Value;
        if (!settings.Enabled || !settings.HasApiKey)
        {
            return NotConfiguredResult(1);
        }

        var status = (await BuildStatusesAsync(cancellationToken))
            .FirstOrDefault(item => item.DogId == dogId);
        if (status is null)
        {
            return new EmbeddingRebuildResultDto(1, 0, 1, 0, "Dog was not found.", settings.Enabled && settings.HasApiKey);
        }

        if (!status.IsPublicSafe)
        {
            return new EmbeddingRebuildResultDto(1, 0, 0, 1, "Dog is not public-safe, so it is skipped by semantic search.", true);
        }

        if (status.LifecycleStatus == EmbeddingLifecycleStatus.UpToDate)
        {
            return new EmbeddingRebuildResultDto(1, 0, 0, 1, "Dog embedding is already up to date.", true);
        }

        var success = await RefreshOneAsync(dogId, cancellationToken);
        return success
            ? new EmbeddingRebuildResultDto(1, 1, 0, 0, "Dog embedding rebuilt.", true)
            : new EmbeddingRebuildResultDto(1, 0, 1, 0, "Dog embedding could not be rebuilt.", true);
    }

    public async Task<EmbeddingRebuildResultDto> RebuildMissingEmbeddingsAsync(
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId);

        var settings = options.Value;
        if (!settings.Enabled || !settings.HasApiKey)
        {
            var missing = await CountPublicSafeNotUpToDateAsync(cancellationToken);
            return NotConfiguredResult(missing);
        }

        var statuses = await BuildStatusesAsync(cancellationToken);
        var dogIds = statuses
            .Where(status => status.LifecycleStatus is EmbeddingLifecycleStatus.Missing or EmbeddingLifecycleStatus.OpenAiDisabled)
            .Select(status => status.DogId)
            .ToList();

        return await RefreshManyAsync(dogIds, "Missing dog embeddings rebuilt.", cancellationToken);
    }

    public async Task<EmbeddingRebuildResultDto> RebuildStaleEmbeddingsAsync(
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId);

        var settings = options.Value;
        if (!settings.Enabled || !settings.HasApiKey)
        {
            var stale = (await BuildStatusesAsync(cancellationToken))
                .Count(status => status.LifecycleStatus == EmbeddingLifecycleStatus.Stale);
            return NotConfiguredResult(stale);
        }

        var dogIds = (await BuildStatusesAsync(cancellationToken))
            .Where(status => status.LifecycleStatus == EmbeddingLifecycleStatus.Stale)
            .Select(status => status.DogId)
            .ToList();

        return await RefreshManyAsync(dogIds, "Stale dog embeddings rebuilt.", cancellationToken);
    }

    public async Task<EmbeddingRebuildResultDto> RebuildAllPublicSafeEmbeddingsAsync(
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId);

        var settings = options.Value;
        if (!settings.Enabled || !settings.HasApiKey)
        {
            var count = await context.Dogs
                .CountAsync(dog => dog.Status == DogStatus.Available || dog.Status == DogStatus.Reserved, cancellationToken);
            return NotConfiguredResult(count);
        }

        var result = await embeddingService.RebuildDogSearchIndexAsync(cancellationToken);
        return new EmbeddingRebuildResultDto(
            result.SearchableDogCount,
            result.Created + result.Updated + result.SkippedUnchanged,
            result.Failed,
            result.Removed,
            result.HasFailures
                ? "Dog search index rebuild completed with some failures."
                : "Dog search index rebuild completed.",
            result.IsConfigured);
    }

    public async Task<SearchDocumentPreviewDto> GetSearchDocumentPreviewAsync(
        int dogId,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(adminUserId);

        var dog = await LoadDogForDocumentAsync(dogId, cancellationToken);
        if (dog is null)
        {
            throw new InvalidOperationException("Dog was not found.");
        }

        if (!IsPublicSafe(dog))
        {
            throw new InvalidOperationException("This dog is not public-safe and is not included in the semantic search index.");
        }

        var content = documentService.BuildDocument(dog);
        return new SearchDocumentPreviewDto(
            dog.Id,
            dog.Name,
            content,
            documentService.ComputeContentHash(content),
            content.Length);
    }

    private async Task<List<DogEmbeddingStatusDto>> BuildStatusesAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var currentModel = settings.GetSafeEmbeddingModel();
        var configured = settings.Enabled && settings.HasApiKey;
        var dogs = await context.Dogs
            .Include(dog => dog.Shelter)
            .Include(dog => dog.DogBreed)
            .Include(dog => dog.SecondaryBreed)
            .Include(dog => dog.PreferredFoodType)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var embeddings = await context.DogSearchEmbeddings
            .AsNoTracking()
            .ToDictionaryAsync(embedding => embedding.DogId, cancellationToken);

        var rows = new List<DogEmbeddingStatusDto>();
        foreach (var dog in dogs)
        {
            embeddings.TryGetValue(dog.Id, out var embedding);
            rows.Add(BuildStatus(dog, embedding, currentModel, configured));
        }

        return rows;
    }

    private DogEmbeddingStatusDto BuildStatus(
        Dog dog,
        DogSearchEmbedding? embedding,
        string currentModel,
        bool embeddingsConfigured)
    {
        var publicSafe = IsPublicSafe(dog);
        var content = publicSafe ? documentService.BuildDocument(dog) : string.Empty;
        var currentHash = publicSafe ? documentService.ComputeContentHash(content) : string.Empty;
        var hashMatches = publicSafe && embedding is not null && embedding.ContentHash == currentHash;
        var modelMatches = publicSafe && embedding is not null &&
            string.Equals(embedding.EmbeddingModel, currentModel, StringComparison.Ordinal);
        var hasVector = !string.IsNullOrWhiteSpace(embedding?.EmbeddingJson);
        var status = ResolveStatus(publicSafe, embedding, embeddingsConfigured, hashMatches, modelMatches, hasVector);

        return new DogEmbeddingStatusDto(
            dog.Id,
            dog.Name,
            dog.Status,
            dog.Shelter?.Name,
            status,
            embedding?.UpdatedAt,
            embedding?.EmbeddingModel,
            hashMatches,
            modelMatches,
            content.Length,
            currentHash,
            embedding?.ContentHash,
            publicSafe,
            BuildStatusReason(status, embedding, hashMatches, modelMatches, hasVector));
    }

    private static EmbeddingLifecycleStatus ResolveStatus(
        bool publicSafe,
        DogSearchEmbedding? embedding,
        bool embeddingsConfigured,
        bool hashMatches,
        bool modelMatches,
        bool hasVector)
    {
        if (!publicSafe)
        {
            return EmbeddingLifecycleStatus.NotPublicSafe;
        }

        if (embedding is null)
        {
            return embeddingsConfigured ? EmbeddingLifecycleStatus.Missing : EmbeddingLifecycleStatus.OpenAiDisabled;
        }

        if (!hasVector)
        {
            return EmbeddingLifecycleStatus.Failed;
        }

        if (!hashMatches || !modelMatches)
        {
            return EmbeddingLifecycleStatus.Stale;
        }

        return EmbeddingLifecycleStatus.UpToDate;
    }

    private static string BuildStatusReason(
        EmbeddingLifecycleStatus status,
        DogSearchEmbedding? embedding,
        bool hashMatches,
        bool modelMatches,
        bool hasVector)
    {
        return status switch
        {
            EmbeddingLifecycleStatus.UpToDate => "Stored embedding matches the current document hash and model.",
            EmbeddingLifecycleStatus.Missing => "No embedding row exists for this public-safe dog.",
            EmbeddingLifecycleStatus.OpenAiDisabled => "Embedding generation is not configured; keyword/rule fallback remains available.",
            EmbeddingLifecycleStatus.Failed when !hasVector => "An embedding row exists but has no vector data.",
            EmbeddingLifecycleStatus.Stale when embedding is not null && !hashMatches => "Dog profile search document changed since the last embedding.",
            EmbeddingLifecycleStatus.Stale when embedding is not null && !modelMatches => "Stored embedding model differs from the configured embedding model.",
            EmbeddingLifecycleStatus.NotPublicSafe => "Dog is not Available or Reserved, so public search does not index it.",
            _ => "Status could not be determined."
        };
    }

    private async Task<Dog?> LoadDogForDocumentAsync(int dogId, CancellationToken cancellationToken)
    {
        return await context.Dogs
            .Include(dog => dog.Shelter)
            .Include(dog => dog.DogBreed)
            .Include(dog => dog.SecondaryBreed)
            .Include(dog => dog.PreferredFoodType)
            .FirstOrDefaultAsync(dog => dog.Id == dogId, cancellationToken);
    }

    private async Task<EmbeddingRebuildResultDto> RefreshManyAsync(
        IReadOnlyList<int> dogIds,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (dogIds.Count == 0)
        {
            return new EmbeddingRebuildResultDto(0, 0, 0, 0, "No embeddings matched this rebuild action.", true);
        }

        var succeeded = 0;
        var failed = 0;
        foreach (var dogId in dogIds)
        {
            if (await RefreshOneAsync(dogId, cancellationToken))
            {
                succeeded++;
            }
            else
            {
                failed++;
            }
        }

        return new EmbeddingRebuildResultDto(
            dogIds.Count,
            succeeded,
            failed,
            0,
            failed == 0 ? successMessage : $"{successMessage} {failed} dog(s) could not be refreshed.",
            true);
    }

    private async Task<bool> RefreshOneAsync(int dogId, CancellationToken cancellationToken)
    {
        try
        {
            return await embeddingService.RefreshDogEmbeddingAsync(dogId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Search index dashboard failed to refresh embedding for DogId {DogId}.", dogId);
            return false;
        }
    }

    private async Task<int> CountPublicSafeNotUpToDateAsync(CancellationToken cancellationToken)
    {
        var statuses = await BuildStatusesAsync(cancellationToken);
        return statuses.Count(status =>
            status.IsPublicSafe &&
            status.LifecycleStatus != EmbeddingLifecycleStatus.UpToDate);
    }

    private async Task EnsureAdminAsync(string adminUserId)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new InvalidOperationException("Current admin user could not be resolved.");
        }

        var user = await userManager.FindByIdAsync(adminUserId);
        if (user is null || !await userManager.IsInRoleAsync(user, IdentitySeedData.AdminRole))
        {
            throw new InvalidOperationException("Only administrators can manage the semantic search index.");
        }
    }

    private static bool IsPublicSafe(Dog dog)
    {
        return dog.Status is DogStatus.Available or DogStatus.Reserved;
    }

    private static int GetStatusSort(EmbeddingLifecycleStatus status)
    {
        return status switch
        {
            EmbeddingLifecycleStatus.Failed => 0,
            EmbeddingLifecycleStatus.Stale => 1,
            EmbeddingLifecycleStatus.Missing => 2,
            EmbeddingLifecycleStatus.OpenAiDisabled => 3,
            EmbeddingLifecycleStatus.UpToDate => 4,
            EmbeddingLifecycleStatus.NotPublicSafe => 5,
            _ => 6
        };
    }

    private static EmbeddingRebuildResultDto NotConfiguredResult(int requested)
    {
        return new EmbeddingRebuildResultDto(
            requested,
            0,
            0,
            requested,
            "OpenAI embeddings are disabled or missing an API key. Keyword/rule fallback remains available.",
            false);
    }
}
