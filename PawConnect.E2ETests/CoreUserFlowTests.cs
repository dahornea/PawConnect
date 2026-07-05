using System.Text.RegularExpressions;
using Microsoft.Playwright;
using PawConnect.E2ETests.Infrastructure;

namespace PawConnect.E2ETests;

public class CoreUserFlowTests : PawConnectPageTest
{
    [E2EFact]
    public async Task Adopter_ShouldBrowseDogs_AndOpenDogDetails()
    {
        await LoginAsync(DemoUsers.Adopter, "/dogs");

        await ExpectTextVisibleAsync("Dogs");
        await ExpectTextVisibleAsync("Browse dogs currently available or reserved");

        await Page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("View details for", RegexOptions.IgnoreCase) })
            .First
            .ClickAsync();

        await Page.WaitForURLAsync(new Regex(".*/dogs/[0-9]+.*", RegexOptions.IgnoreCase));
        await ExpectTextVisibleAsync("Back to Dogs");
        await ExpectTextVisibleAsync("Size");
        await ExpectTextVisibleAsync("Location");
    }

    [E2EFact]
    public async Task Shelter_ShouldViewOrManageOwnDogs()
    {
        await LoginAsync(DemoUsers.Shelter, "/shelter/dogs");

        await ExpectTextVisibleAsync("Manage Dogs");
        await ExpectTextVisibleAsync("Dog records");
        await ExpectTextVisibleAsync("Add Dog");
        await ExpectTextVisibleAsync("Export CSV");
    }

    [E2EFact]
    public async Task Admin_ShouldOpenDashboard_AndSeeSummaryCards()
    {
        await LoginAsync(DemoUsers.Admin, "/admin/dashboard");

        await ExpectTextVisibleAsync("Admin Dashboard");
        await ExpectTextVisibleAsync("Users");
        await ExpectTextVisibleAsync("Shelters");
        await ExpectTextVisibleAsync("Dogs");
        await ExpectTextVisibleAsync("Pending requests");
    }

    [E2EFact]
    public async Task Admin_ShouldOpenNotificationOutbox_WhenFeatureExists()
    {
        await LoginAsync(DemoUsers.Admin, "/admin/notification-outbox");

        await ExpectTextVisibleAsync("Notification Outbox");
        await ExpectTextVisibleAsync("Monitor queued notification work");
        await ExpectTextVisibleAsync("Dead letter");
        await ExpectRoleVisibleAsync(AriaRole.Button, "Process due now");
    }
}
