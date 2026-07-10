using Microsoft.Playwright;
using PawConnect.E2ETests.Infrastructure;

namespace PawConnect.E2ETests;

public class AuthenticationAndRoleNavigationTests : PawConnectPageTest
{
    [E2EFact]
    public async Task Login_ShouldNavigateToDashboard_ForValidUser()
    {
        await LoginAsync(DemoUsers.Adopter);

        await ExpectTextVisibleAsync("Adopter workspace");
        await ExpectTextVisibleAsync("Welcome, Ana Ionescu");
        await ExpectTextVisibleAsync("Favorite dogs");
    }

    [E2EFact]
    public async Task Navigation_ShouldShowAdminPages_ForAdminUser()
    {
        await LoginAsync(DemoUsers.Admin);

        await ExpectTextVisibleAsync("Admin Dashboard");
        await ExpectTextVisibleAsync("System overview");
        await ExpectRoleVisibleAsync(AriaRole.Link, "Users");
        await ExpectRoleVisibleAsync(AriaRole.Link, "Notification Outbox");
    }

    [E2EFact]
    public async Task Navigation_ShouldShowShelterPages_ForShelterUser()
    {
        await LoginAsync(DemoUsers.Shelter);

        await ExpectTextVisibleAsync("Shelter Dashboard");
        await ExpectTextVisibleAsync("Shelter operations");
        await ExpectRoleVisibleAsync(AriaRole.Link, "Manage Dogs");
        await ExpectRoleVisibleAsync(AriaRole.Link, "Appointments");
    }

    [E2EFact]
    public async Task Navigation_ShouldShowAdopterPages_ForAdopterUser()
    {
        await LoginAsync(DemoUsers.Adopter);

        await ExpectTextVisibleAsync("Adopter workspace");
        await ExpectRoleVisibleAsync(AriaRole.Link, "Recommendations");
        await ExpectRoleVisibleAsync(AriaRole.Link, "Copilot");
        await ExpectRoleVisibleAsync(AriaRole.Link, "My Requests");
    }

    [E2EFact]
    public async Task Navigation_ShouldNotShowAdminPages_ForAdopterUser()
    {
        await LoginAsync(DemoUsers.Adopter);

        await Page.GetByText("Admin", new() { Exact = true }).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached,
            Timeout = 2_000
        });
    }

    [E2EFact]
    public async Task Shelter_ShouldOpenOperationsIntelligence_FromRoleNavigation()
    {
        await LoginAsync(DemoUsers.Shelter);

        await Page.GetByRole(AriaRole.Link, new() { Name = "Operations Intelligence" }).ClickAsync();

        await ExpectTextVisibleAsync("Operations Intelligence");
        await ExpectTextVisibleAsync("Explainable decision support");
    }

    [E2EFact]
    public async Task Admin_ShouldOpenPlatformIntelligence_FromRoleNavigation()
    {
        await LoginAsync(DemoUsers.Admin);

        await Page.GetByRole(AriaRole.Link, new() { Name = "Platform Intelligence" }).ClickAsync();

        await ExpectTextVisibleAsync("Platform Intelligence");
        await ExpectTextVisibleAsync("Explainable decision support");
    }

    [E2EFact]
    public async Task Adopter_ShouldOpenOwnInsights_FromRoleNavigation()
    {
        await LoginAsync(DemoUsers.Adopter);

        await Page.GetByRole(AriaRole.Link, new() { Name = "My Insights" }).ClickAsync();

        await ExpectTextVisibleAsync("Your Next Steps");
        await ExpectTextVisibleAsync("Explainable decision support");
    }
}
