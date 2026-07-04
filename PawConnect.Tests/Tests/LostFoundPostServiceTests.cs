using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class LostFoundPostServiceTests
{
    [Fact]
    public async Task CreatePostAsync_SavesPendingPostAndNotifiesAdmins()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var service = CreateService(databaseName);

        var created = await service.CreatePostAsync(CreateRequest(), TestDbContextFactory.AdopterId);

        var saved = await context.LostFoundPosts.Include(post => post.Images).SingleAsync();
        Assert.Equal(LostFoundPostStatus.PendingReview, saved.Status);
        Assert.Equal(TestDbContextFactory.AdopterId, saved.CreatedByUserId);
        Assert.Single(saved.Images);
        Assert.True(saved.Images.Single().IsMain);
        Assert.Equal(created.Id, saved.Id);
        Assert.True(await context.Notifications.AnyAsync(notification =>
            notification.UserId == TestDbContextFactory.AdminId &&
            notification.RelatedEntityName == nameof(LostFoundPost)));
    }

    [Fact]
    public async Task PublicPosts_OnlyShowApprovedPostsByDefault()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        await using var context = TestDbContextFactory.CreateContext(databaseName);
        var service = CreateService(databaseName);
        var pending = await service.CreatePostAsync(CreateRequest(title: "Pending dog"), TestDbContextFactory.AdopterId);
        var approved = await service.CreatePostAsync(CreateRequest(title: "Approved dog"), TestDbContextFactory.AdopterId);

        await service.ApprovePostAsync(approved.Id, TestDbContextFactory.AdminId);

        var publicPosts = await service.GetPublicPostsAsync(new LostFoundPostFilter());

        Assert.DoesNotContain(publicPosts, post => post.Id == pending.Id);
        Assert.Contains(publicPosts, post => post.Id == approved.Id);
    }

    [Fact]
    public async Task GetVisibleDetailsAsync_AllowsCreatorAndAdminButBlocksUnrelatedPendingPost()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        TestDbContextFactory.CreateContext(databaseName).Dispose();
        var service = CreateService(databaseName);
        var post = await service.CreatePostAsync(CreateRequest(), TestDbContextFactory.AdopterId);

        var creatorView = await service.GetVisibleDetailsAsync(post.Id, TestDbContextFactory.AdopterId, isAdmin: false);
        var unrelatedView = await service.GetVisibleDetailsAsync(post.Id, TestDbContextFactory.SecondAdopterId, isAdmin: false);
        var adminView = await service.GetVisibleDetailsAsync(post.Id, TestDbContextFactory.AdminId, isAdmin: true);

        Assert.NotNull(creatorView);
        Assert.Null(unrelatedView);
        Assert.NotNull(adminView);
    }

    [Fact]
    public async Task AdminModeration_ApproveRejectCloseAndReopenUpdateStatus()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        TestDbContextFactory.CreateContext(databaseName).Dispose();
        var service = CreateService(databaseName);
        var post = await service.CreatePostAsync(CreateRequest(), TestDbContextFactory.AdopterId);

        var rejected = await service.RejectPostAsync(post.Id, "Image is unclear.", TestDbContextFactory.AdminId);
        var approved = await service.ApprovePostAsync(post.Id, TestDbContextFactory.AdminId);
        var closed = await service.ClosePostAsync(post.Id, "Dog returned home.", TestDbContextFactory.AdminId, isAdmin: true);
        var reopened = await service.ReopenPostAsync(post.Id, TestDbContextFactory.AdminId);

        Assert.Equal(LostFoundPostStatus.Rejected, rejected.Status);
        Assert.Equal("Image is unclear.", rejected.RejectionReason);
        Assert.Equal(LostFoundPostStatus.Approved, approved.Status);
        Assert.Equal(LostFoundPostStatus.Closed, closed.Status);
        Assert.Equal("Dog returned home.", closed.ResolutionNotes);
        Assert.Equal(LostFoundPostStatus.Approved, reopened.Status);
        Assert.Null(reopened.ResolutionNotes);
    }

    [Fact]
    public async Task CreatePostAsync_RejectsInvalidImagesAndTooManyImages()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        TestDbContextFactory.CreateContext(databaseName).Dispose();
        var service = CreateService(databaseName);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePostAsync(
                CreateRequest(images: [new LostFoundPostImageInputDto("/images/dog-placeholder.svg", true)]),
                TestDbContextFactory.AdopterId));

        var tooManyImages = Enumerable.Range(1, LostFoundPostService.MaxImagesPerPost + 1)
            .Select(index => new LostFoundPostImageInputDto($"https://example.com/dog-{index}.jpg", index == 1))
            .ToList();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePostAsync(CreateRequest(images: tooManyImages), TestDbContextFactory.AdopterId));
    }

    [Fact]
    public async Task AdminMethods_RejectNonAdminUser()
    {
        var databaseName = TestDbContextFactory.CreateDatabaseName();
        TestDbContextFactory.CreateContext(databaseName).Dispose();
        var service = CreateService(databaseName);
        var post = await service.CreatePostAsync(CreateRequest(), TestDbContextFactory.AdopterId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApprovePostAsync(post.Id, TestDbContextFactory.AdopterId));
    }

    private static LostFoundPostService CreateService(string databaseName)
    {
        var factory = TestDbContextFactory.CreateContextFactory(databaseName);
        var notifications = new NotificationService(factory, NullLogger<NotificationService>.Instance);
        return new LostFoundPostService(factory, notifications);
    }

    private static LostFoundPostCreateRequest CreateRequest(
        string title = "Lost brown dog near the park",
        IReadOnlyList<LostFoundPostImageInputDto>? images = null)
    {
        return new LostFoundPostCreateRequest(
            LostFoundPostType.Lost,
            title,
            "Small brown dog last seen close to the park entrance. He is shy and wears a blue collar.",
            "Rudi",
            "Terrier mix",
            DogSize.Small,
            "Brown",
            "Blue collar",
            DateTime.UtcNow.Date,
            "Cluj-Napoca",
            "Zorilor",
            "Near the park entrance",
            46.753,
            23.588,
            "Ana Ionescu",
            "ana@example.com",
            "+40 700 000 101",
            true,
            images ?? [new LostFoundPostImageInputDto("https://example.com/lost-dog.jpg", true)]);
    }
}
