using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class SearchIndexDashboardServiceTests
{
    [Fact]
    public async Task GetDogEmbeddingStatusesAsync_ClassifiesEmbeddingLifecycleStates()
    {
        using var context = TestDbContextFactory.CreateContext();
        var documentService = new DogSearchDocumentService();
        var settings = EnabledOpenAiSettings();
        var upToDateDog = AddDog(context, "Up To Date");
        AddCurrentEmbedding(context, documentService, upToDateDog, settings.GetSafeEmbeddingModel());
        var missingDog = AddDog(context, "Missing");
        var staleHashDog = AddDog(context, "Changed Profile");
        AddEmbedding(context, documentService, staleHashDog, "old-hash", settings.GetSafeEmbeddingModel());
        var staleModelDog = AddDog(context, "Old Model");
        AddCurrentEmbedding(context, documentService, staleModelDog, "older-embedding-model");
        var adoptedDog = AddDog(context, "Adopted Dog", DogStatus.Adopted);

        var service = CreateService(context, settings: settings);

        var statuses = await service.GetDogEmbeddingStatusesAsync();

        Assert.Equal(EmbeddingLifecycleStatus.UpToDate, Find(statuses, upToDateDog.Id).LifecycleStatus);
        Assert.Equal(EmbeddingLifecycleStatus.Missing, Find(statuses, missingDog.Id).LifecycleStatus);
        Assert.Equal(EmbeddingLifecycleStatus.Stale, Find(statuses, staleHashDog.Id).LifecycleStatus);
        Assert.Equal(EmbeddingLifecycleStatus.Stale, Find(statuses, staleModelDog.Id).LifecycleStatus);
        Assert.Equal(EmbeddingLifecycleStatus.NotPublicSafe, Find(statuses, adoptedDog.Id).LifecycleStatus);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenOpenAiDisabled_ShowsFallbackState()
    {
        using var context = TestDbContextFactory.CreateContext();
        AddDog(context, "Fallback Dog");
        var settings = new OpenAiSettings
        {
            Enabled = false,
            ApiKey = string.Empty,
            EmbeddingModel = "text-embedding-3-small"
        };
        var service = CreateService(context, settings: settings);

        var summary = await service.GetSummaryAsync();
        var statuses = await service.GetDogEmbeddingStatusesAsync();

        Assert.False(summary.EmbeddingsConfigured);
        Assert.True(summary.KeywordFallbackAvailable);
        Assert.Equal(1, summary.MissingCount);
        Assert.Equal(EmbeddingLifecycleStatus.OpenAiDisabled, statuses.Single().LifecycleStatus);
    }

    [Fact]
    public async Task GetSearchDocumentPreviewAsync_ReturnsPublicSafeDocumentWithoutShelterContact()
    {
        using var context = TestDbContextFactory.CreateContext();
        var dog = AddDog(context, "Preview Dog");
        var service = CreateService(context);

        var preview = await service.GetSearchDocumentPreviewAsync(dog.Id, TestDbContextFactory.AdminId);

        Assert.Contains("Preview Dog", preview.Content);
        Assert.DoesNotContain("shelter@test.com", preview.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("123", preview.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(64, preview.ContentHash.Length);
    }

    [Fact]
    public async Task RebuildMissingEmbeddingsAsync_AsAdmin_CreatesOnlyMissingEmbeddings()
    {
        using var context = TestDbContextFactory.CreateContext();
        var documentService = new DogSearchDocumentService();
        var settings = EnabledOpenAiSettings();
        var upToDateDog = AddDog(context, "Already Indexed");
        AddCurrentEmbedding(context, documentService, upToDateDog, settings.GetSafeEmbeddingModel());
        var missingDog = AddDog(context, "Needs Index");
        var fakeEmbeddingService = new FakeEmbeddingService();
        var service = CreateService(context, fakeEmbeddingService, settings);

        var result = await service.RebuildMissingEmbeddingsAsync(TestDbContextFactory.AdminId);

        Assert.Equal(1, result.Requested);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Equal(1, fakeEmbeddingService.GenerateCallCount);
        Assert.True(await context.DogSearchEmbeddings.AnyAsync(embedding => embedding.DogId == missingDog.Id));
    }

    [Fact]
    public async Task RebuildMissingEmbeddingsAsync_RejectsNonAdminUser()
    {
        using var context = TestDbContextFactory.CreateContext();
        AddDog(context, "Protected Dog");
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RebuildMissingEmbeddingsAsync(TestDbContextFactory.ShelterUserId));
    }

    private static SearchIndexDashboardService CreateService(
        ApplicationDbContext context,
        FakeEmbeddingService? fakeEmbeddingService = null,
        OpenAiSettings? settings = null)
    {
        settings ??= EnabledOpenAiSettings();
        fakeEmbeddingService ??= new FakeEmbeddingService();
        var documentService = new DogSearchDocumentService();
        var embeddingService = new DogSearchEmbeddingService(
            context,
            documentService,
            fakeEmbeddingService,
            Options.Create(settings),
            NullLogger<DogSearchEmbeddingService>.Instance);

        return new SearchIndexDashboardService(
            context,
            documentService,
            embeddingService,
            Options.Create(settings),
            TestDbContextFactory.CreateUserManager(context),
            NullLogger<SearchIndexDashboardService>.Instance);
    }

    private static Dog AddDog(ApplicationDbContext context, string name, DogStatus status = DogStatus.Available)
    {
        var dog = TestDbContextFactory.CreateDog(name, status);
        dog.Location = "Cluj-Napoca";
        dog.Description = $"{name} is a calm public dog with a predictable routine.";
        context.Dogs.Add(dog);
        context.SaveChanges();
        return dog;
    }

    private static void AddCurrentEmbedding(
        ApplicationDbContext context,
        DogSearchDocumentService documentService,
        Dog dog,
        string embeddingModel)
    {
        var content = documentService.BuildDocument(dog);
        AddEmbedding(context, documentService, dog, documentService.ComputeContentHash(content), embeddingModel);
    }

    private static void AddEmbedding(
        ApplicationDbContext context,
        DogSearchDocumentService documentService,
        Dog dog,
        string contentHash,
        string embeddingModel)
    {
        var content = documentService.BuildDocument(dog);
        context.DogSearchEmbeddings.Add(new DogSearchEmbedding
        {
            DogId = dog.Id,
            Content = content,
            ContentHash = contentHash,
            EmbeddingJson = "[0.1,0.2]",
            EmbeddingModel = embeddingModel,
            UpdatedAt = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    private static DogEmbeddingStatusDto Find(IReadOnlyList<DogEmbeddingStatusDto> statuses, int dogId)
    {
        return statuses.Single(status => status.DogId == dogId);
    }

    private static OpenAiSettings EnabledOpenAiSettings()
    {
        return new OpenAiSettings
        {
            Enabled = true,
            ApiKey = "test-key",
            EmbeddingModel = "text-embedding-3-small"
        };
    }

    private sealed class FakeEmbeddingService : IEmbeddingService
    {
        public int GenerateCallCount { get; private set; }

        public Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            GenerateCallCount++;
            return Task.FromResult<float[]?>([0.1f, 0.2f]);
        }

        public double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            return 1;
        }
    }
}
