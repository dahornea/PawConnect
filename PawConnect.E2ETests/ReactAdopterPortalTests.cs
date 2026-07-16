using System.Text.RegularExpressions;
using Microsoft.Playwright;
using PawConnect.E2ETests.Infrastructure;

namespace PawConnect.E2ETests;

public class ReactAdopterPortalTests : PawConnectPageTest
{
    [ReactPortalE2EFact]
    public async Task PublicPortal_ShouldFilterDogs_AndOpenDetails()
    {
        await Page.GotoAsync(ReactAppUrl("/dogs"));
        await ExpectTextVisibleAsync("Find your next companion");

        await Page.GetByLabel("Size").SelectOptionAsync("Small");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Apply filters" }).ClickAsync();
        await Page.WaitForURLAsync(new Regex(".*size=Small.*", RegexOptions.IgnoreCase));
        await ExpectRoleVisibleAsync(AriaRole.Button, "Remove size filter");

        var firstCard = Page.Locator("article").First;
        await firstCard.GetByRole(AriaRole.Link, new() { Name = "View details" }).ClickAsync();
        await Page.WaitForURLAsync(new Regex(".*/dogs/[0-9]+.*", RegexOptions.IgnoreCase));
        await ExpectTextVisibleAsync("About");
    }

    [ReactPortalE2EFact]
    public async Task Adopter_ShouldSignIn_OpenFavorites_AndSignOut()
    {
        await Page.GotoAsync(ReactAppUrl("/favorites"));
        await Page.WaitForURLAsync(new Regex(".*/login.*", RegexOptions.IgnoreCase));

        await Page.GetByLabel("Email").FillAsync(DemoUsers.Adopter.Email);
        await Page.GetByLabel("Password").FillAsync(DemoUsers.Adopter.Password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();

        await Page.WaitForURLAsync(new Regex(".*/favorites$", RegexOptions.IgnoreCase));
        await ExpectTextVisibleAsync("Favorite dogs");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        await Page.WaitForURLAsync(new Regex(".*/$", RegexOptions.IgnoreCase));
        await ExpectRoleVisibleAsync(AriaRole.Link, "Sign in");

        await Page.GotoAsync(ReactAppUrl("/favorites"));
        await Page.WaitForURLAsync(new Regex(".*/login.*", RegexOptions.IgnoreCase));
    }

    [ReactPortalE2EFact]
    public async Task ShelterAccount_ShouldBeRejectedByAdopterPortal()
    {
        await Page.GotoAsync(ReactAppUrl("/login"));
        await Page.GetByLabel("Email").FillAsync(DemoUsers.Shelter.Email);
        await Page.GetByLabel("Password").FillAsync(DemoUsers.Shelter.Password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();

        await ExpectTextVisibleAsync("available to adopter accounts only");
        await Page.WaitForURLAsync(new Regex(".*/login.*", RegexOptions.IgnoreCase));
    }
}
