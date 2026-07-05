using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace PawConnect.E2ETests.Infrastructure;

public abstract class PawConnectPageTest : IAsyncLifetime
{
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IBrowserContext? context;

    protected IPage Page { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = E2ETestSettings.Headless
        });
        context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1440, Height = 1000 }
        });

        Page = await context.NewPageAsync();
        Page.SetDefaultTimeout(E2ETestSettings.TimeoutMs);
        Page.SetDefaultNavigationTimeout(E2ETestSettings.TimeoutMs);
    }

    public async Task DisposeAsync()
    {
        if (context is not null)
        {
            await context.DisposeAsync();
        }

        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
    }

    protected static string AppUrl(string path)
    {
        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{E2ETestSettings.BaseUrl}{normalizedPath}";
    }

    protected async Task LoginAsync(DemoUser user, string? returnPath = null)
    {
        var targetPath = returnPath ?? user.DashboardPath;
        await Page.GotoAsync(AppUrl($"/Account/Login?returnUrl={Uri.EscapeDataString(targetPath)}"));
        await Page.Locator("#Input\\.Email").FillAsync(user.Email);
        await Page.Locator("#Input\\.Password").FillAsync(user.Password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await Page.WaitForURLAsync(new Regex($".*{Regex.Escape(targetPath)}.*", RegexOptions.IgnoreCase));
    }

    protected async Task ExpectTextVisibleAsync(string text)
    {
        await Page.GetByText(text, new() { Exact = false })
            .First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    protected async Task ExpectRoleVisibleAsync(AriaRole role, string name)
    {
        await Page.GetByRole(role, new() { Name = name })
            .First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }
}
