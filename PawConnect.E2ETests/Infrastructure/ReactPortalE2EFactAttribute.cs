using System.Net;

namespace PawConnect.E2ETests.Infrastructure;

public sealed class ReactPortalE2EFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> SkipReason = new(ResolveSkipReason);

    public ReactPortalE2EFactAttribute()
    {
        Skip = SkipReason.Value;
    }

    private static string? ResolveSkipReason()
    {
        if (!E2ETestSettings.ShouldRun)
        {
            return $"Browser E2E tests are opt-in. Set {E2ETestSettings.RunEnvironmentVariable}=1 and start PawConnect and the React portal.";
        }

        return IsReactPortalAvailable()
            ? null
            : $"The React adopter portal is not reachable at {E2ETestSettings.ReactBaseUrl}. Start Vite and verify /health through its backend proxy.";
    }

    private static bool IsReactPortalAvailable()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = client.GetAsync($"{E2ETestSettings.ReactBaseUrl}/health").GetAwaiter().GetResult();
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch
        {
            return false;
        }
    }
}
