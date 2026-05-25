using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class DogSearchEmbeddingService(
    ApplicationDbContext context,
    IDogSearchDocumentService documentService,
    IEmbeddingService embeddingService,
    IOptions<OpenAiSettings> options,
    ILogger<DogSearchEmbeddingService> logger) : IDogSearchEmbeddingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> RefreshDogEmbeddingAsync(int dogId, CancellationToken cancellationToken = default)
    {
        var result = await RefreshDogEmbeddingCoreAsync(dogId, options.Value, cancellationToken);
        return result is DogEmbeddingRefreshOutcome.Created or DogEmbeddingRefreshOutcome.Updated or DogEmbeddingRefreshOutcome.Removed;
    }

    public async Task<int> RefreshMissingDogEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        LogOpenAiEmbeddingConfiguration(settings);
        if (!settings.Enabled || !settings.HasApiKey)
        {
            return 0;
        }

        var dogIds = await context.Dogs
            .Where(d => d.Status == DogStatus.Available || d.Status == DogStatus.Reserved)
            .Where(d => !context.DogSearchEmbeddings.Any(e => e.DogId == d.Id))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var createdOrUpdated = 0;
        foreach (var dogId in dogIds)
        {
            var result = await RefreshDogEmbeddingCoreAsync(dogId, settings, cancellationToken);
            if (result is DogEmbeddingRefreshOutcome.Created or DogEmbeddingRefreshOutcome.Updated)
            {
                createdOrUpdated++;
            }
        }

        return createdOrUpdated;
    }

    public async Task<int> RefreshAllDogEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RebuildDogSearchIndexAsync(cancellationToken);
        return result.GeneratedRows;
    }

    public async Task<DogSearchIndexRefreshResult> RebuildDogSearchIndexAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        LogOpenAiEmbeddingConfiguration(settings);

        var existingCount = await context.DogSearchEmbeddings.CountAsync(cancellationToken);
        var searchableDogIds = await context.Dogs
            .Where(d => d.Status == DogStatus.Available || d.Status == DogStatus.Reserved)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var removed = await RemoveStaleEmbeddingsAsync(cancellationToken);
        if (!settings.Enabled || !settings.HasApiKey)
        {
            logger.LogWarning(
                "Dog search index rebuild skipped. OpenAI embeddings configured: Enabled={Enabled}, HasApiKey={HasApiKey}, EmbeddingModel={EmbeddingModel}.",
                settings.Enabled,
                settings.HasApiKey,
                settings.GetSafeEmbeddingModel());

            return new DogSearchIndexRefreshResult(
                settings.Enabled,
                settings.HasApiKey,
                settings.GetSafeEmbeddingModel(),
                searchableDogIds.Count,
                existingCount,
                0,
                0,
                0,
                removed,
                0);
        }

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var dogId in searchableDogIds)
        {
            var outcome = await RefreshDogEmbeddingCoreAsync(dogId, settings, cancellationToken);
            switch (outcome)
            {
                case DogEmbeddingRefreshOutcome.Created:
                    created++;
                    break;
                case DogEmbeddingRefreshOutcome.Updated:
                    updated++;
                    break;
                case DogEmbeddingRefreshOutcome.Unchanged:
                    skipped++;
                    break;
                case DogEmbeddingRefreshOutcome.Failed:
                    failed++;
                    break;
                case DogEmbeddingRefreshOutcome.Removed:
                    removed++;
                    break;
            }
        }

        return new DogSearchIndexRefreshResult(
            settings.Enabled,
            settings.HasApiKey,
            settings.GetSafeEmbeddingModel(),
            searchableDogIds.Count,
            existingCount,
            created,
            updated,
            skipped,
            removed,
            failed);
    }

    public Task<List<DogSearchEmbedding>> GetSearchableDogEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        return context.DogSearchEmbeddings
            .Include(e => e.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(e => e.Dog)
            .ThenInclude(d => d!.DogBreed)
            .Include(e => e.Dog)
            .ThenInclude(d => d!.SecondaryBreed)
            .Include(e => e.Dog)
            .ThenInclude(d => d!.Images)
            .Include(e => e.Dog)
            .ThenInclude(d => d!.PreferredFoodType)
            .Where(e => e.Dog != null &&
                        (e.Dog.Status == DogStatus.Available || e.Dog.Status == DogStatus.Reserved))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private async Task<DogEmbeddingRefreshOutcome> RefreshDogEmbeddingCoreAsync(
        int dogId,
        OpenAiSettings settings,
        CancellationToken cancellationToken)
    {
        var dog = await context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
            .Include(d => d.PreferredFoodType)
            .FirstOrDefaultAsync(d => d.Id == dogId, cancellationToken);

        var existing = await context.DogSearchEmbeddings
            .FirstOrDefaultAsync(e => e.DogId == dogId, cancellationToken);

        if (dog is null)
        {
            return DogEmbeddingRefreshOutcome.NotSearchable;
        }

        if (dog.Status is not (DogStatus.Available or DogStatus.Reserved))
        {
            if (existing is not null)
            {
                context.DogSearchEmbeddings.Remove(existing);
                await context.SaveChangesAsync(cancellationToken);
                return DogEmbeddingRefreshOutcome.Removed;
            }

            return DogEmbeddingRefreshOutcome.NotSearchable;
        }

        if (!settings.Enabled || !settings.HasApiKey)
        {
            return DogEmbeddingRefreshOutcome.NotConfigured;
        }

        var content = documentService.BuildDocument(dog);
        var contentHash = documentService.ComputeContentHash(content);
        var embeddingModel = settings.GetSafeEmbeddingModel();

        if (existing is not null &&
            existing.ContentHash == contentHash &&
            existing.EmbeddingModel == embeddingModel &&
            !string.IsNullOrWhiteSpace(existing.EmbeddingJson))
        {
            return DogEmbeddingRefreshOutcome.Unchanged;
        }

        float[]? embedding;
        try
        {
            embedding = await embeddingService.GenerateEmbeddingAsync(content, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dog search embedding refresh failed for DogId {DogId}.", dogId);
            return DogEmbeddingRefreshOutcome.Failed;
        }

        if (embedding is null || embedding.Length == 0)
        {
            logger.LogWarning("Dog search embedding refresh skipped for DogId {DogId} because embedding generation failed.", dogId);
            return DogEmbeddingRefreshOutcome.Failed;
        }

        if (existing is null)
        {
            context.DogSearchEmbeddings.Add(new DogSearchEmbedding
            {
                DogId = dog.Id,
                Content = content,
                ContentHash = contentHash,
                EmbeddingJson = JsonSerializer.Serialize(embedding, JsonOptions),
                EmbeddingModel = embeddingModel,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);
            return DogEmbeddingRefreshOutcome.Created;
        }

        existing.Content = content;
        existing.ContentHash = contentHash;
        existing.EmbeddingJson = JsonSerializer.Serialize(embedding, JsonOptions);
        existing.EmbeddingModel = embeddingModel;
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return DogEmbeddingRefreshOutcome.Updated;
    }

    private async Task<int> RemoveStaleEmbeddingsAsync(CancellationToken cancellationToken)
    {
        var staleEmbeddings = await context.DogSearchEmbeddings
            .Include(e => e.Dog)
            .Where(e => e.Dog == null || (e.Dog.Status != DogStatus.Available && e.Dog.Status != DogStatus.Reserved))
            .ToListAsync(cancellationToken);

        if (staleEmbeddings.Count == 0)
        {
            return 0;
        }

        context.DogSearchEmbeddings.RemoveRange(staleEmbeddings);
        await context.SaveChangesAsync(cancellationToken);
        return staleEmbeddings.Count;
    }

    private void LogOpenAiEmbeddingConfiguration(OpenAiSettings settings)
    {
        logger.LogInformation(
            "Dog search embedding configuration: OpenAIEnabled={Enabled}, EmbeddingModel={EmbeddingModel}, HasApiKey={HasApiKey}.",
            settings.Enabled,
            settings.GetSafeEmbeddingModel(),
            settings.HasApiKey);
    }

    private enum DogEmbeddingRefreshOutcome
    {
        Created,
        Updated,
        Unchanged,
        Removed,
        Failed,
        NotSearchable,
        NotConfigured
    }
}
