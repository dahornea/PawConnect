using Microsoft.Playwright;
using PawConnect.E2ETests.Infrastructure;

namespace PawConnect.E2ETests;

public class SmokeTests : PawConnectPageTest
{
    [E2EFact]
    public async Task App_ShouldLoad_LoginPageOrHomePage()
    {
        await Page.GotoAsync(AppUrl("/"));

        await ExpectTextVisibleAsync("PawConnect");
        await ExpectTextVisibleAsync("Match stray dogs with adopters");
        await ExpectRoleVisibleAsync(AriaRole.Link, "Browse Dogs");
    }
}
