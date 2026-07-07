using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class SavedViewServiceTests
{
    [Fact]
    public async Task CreateViewAsync_SavesFilterStateAndSummary()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new SavedViewService(context);

        var view = await service.CreateViewAsync(
            TestDbContextFactory.AdopterId,
            [IdentitySeedData.AdopterRole],
            new SavedViewCreateRequest(
                "Small dogs",
                "Dogs.Search",
                SavedViewRoleScope.Adopter,
                "Small available dogs",
                """{"size":"Small","status":"Available"}""",
                SummaryLabels: ["Size: Small", "Status: Available"]));

        Assert.Equal("Small dogs", view.Name);
        Assert.Equal("Dogs.Search", view.PageKey);
        Assert.Equal("""{"size":"Small","status":"Available"}""", view.FilterStateJson);
        Assert.Contains("Size: Small", view.SummaryLabels);
    }

    [Fact]
    public async Task CreateViewAsync_BlocksDuplicateNameOnSamePageForUser()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new SavedViewService(context);
        var request = new SavedViewCreateRequest(
            "My filters",
            "Dogs.Search",
            SavedViewRoleScope.Adopter,
            null,
            """{"status":"Available"}""");

        await service.CreateViewAsync(TestDbContextFactory.AdopterId, [IdentitySeedData.AdopterRole], request);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateViewAsync(TestDbContextFactory.AdopterId, [IdentitySeedData.AdopterRole], request));
    }

    [Fact]
    public async Task UpdateViewAsync_BlocksAnotherUserView()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new SavedViewService(context);
        var view = await service.CreateViewAsync(
            TestDbContextFactory.AdopterId,
            [IdentitySeedData.AdopterRole],
            new SavedViewCreateRequest("Mine", "Dogs.Search", SavedViewRoleScope.Adopter, null, "{}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateViewAsync(
                view.Id,
                TestDbContextFactory.SecondAdopterId,
                [IdentitySeedData.AdopterRole],
                new SavedViewUpdateRequest("Changed", null, "{}")));
    }

    [Fact]
    public async Task CreateViewAsync_KeepsOnlyOneDefaultPerUserAndPage()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new SavedViewService(context);

        var first = await service.CreateViewAsync(
            TestDbContextFactory.AdopterId,
            [IdentitySeedData.AdopterRole],
            new SavedViewCreateRequest("First", "Dogs.Search", SavedViewRoleScope.Adopter, null, "{}", IsDefault: true));
        var second = await service.CreateViewAsync(
            TestDbContextFactory.AdopterId,
            [IdentitySeedData.AdopterRole],
            new SavedViewCreateRequest("Second", "Dogs.Search", SavedViewRoleScope.Adopter, null, "{}", IsDefault: true));

        var views = await service.GetViewsForPageAsync(
            TestDbContextFactory.AdopterId,
            [IdentitySeedData.AdopterRole],
            "Dogs.Search");

        Assert.False(views.Single(view => view.Id == first.Id).IsDefault);
        Assert.True(views.Single(view => view.Id == second.Id).IsDefault);
    }

    [Fact]
    public async Task CreateViewAsync_RejectsInvalidJsonState()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new SavedViewService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateViewAsync(
                TestDbContextFactory.AdopterId,
                [IdentitySeedData.AdopterRole],
                new SavedViewCreateRequest("Bad JSON", "Dogs.Search", SavedViewRoleScope.Adopter, null, "{bad")));
    }

    [Fact]
    public async Task GetPinnedViewsAsync_ReturnsOnlyPinnedAccessibleViews()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new SavedViewService(context);

        var pinned = await service.CreateViewAsync(
            TestDbContextFactory.AdopterId,
            [IdentitySeedData.AdopterRole],
            new SavedViewCreateRequest("Pinned", "Dogs.Search", SavedViewRoleScope.Adopter, null, "{}", IsPinned: true));
        await service.CreateViewAsync(
            TestDbContextFactory.AdopterId,
            [IdentitySeedData.AdopterRole],
            new SavedViewCreateRequest("Not pinned", "Dogs.Search", SavedViewRoleScope.Adopter, null, "{}"));

        var views = await service.GetPinnedViewsAsync(TestDbContextFactory.AdopterId, [IdentitySeedData.AdopterRole]);

        Assert.Contains(views, view => view.Id == pinned.Id);
        Assert.DoesNotContain(views, view => view.Name == "Not pinned");
    }

    [Fact]
    public async Task GetViewsForPageAsync_IncludesAccessibleSystemViews()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.UserSavedViews.Add(new UserSavedView
        {
            Name = "Failed Notifications",
            PageKey = "Admin.Notifications.Outbox",
            RoleScope = SavedViewRoleScope.Admin,
            FilterStateJson = """{"status":"Failed"}""",
            IsSystemView = true
        });
        await context.SaveChangesAsync();
        var service = new SavedViewService(context);

        var views = await service.GetViewsForPageAsync(
            TestDbContextFactory.AdminId,
            [IdentitySeedData.AdminRole],
            "Admin.Notifications.Outbox");

        Assert.Contains(views, view => view.Name == "Failed Notifications" && view.IsSystemView);
    }
}
