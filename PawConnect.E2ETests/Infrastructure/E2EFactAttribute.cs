using System.Net;

namespace PawConnect.E2ETests.Infrastructure;

public sealed class E2EFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> SkipReason = new(ResolveSkipReason);

    public E2EFactAttribute()
    {
        Skip = SkipReason.Value;
    }

    private static string? ResolveSkipReason()
    {
        if (!E2ETestSettings.ShouldRun)
        {
            return $"Browser E2E tests are opt-in. Set {E2ETestSettings.RunEnvironmentVariable}=1 and start PawConnect to run them.";
        }

        return IsPawConnectAvailable()
            ? null
            : $"PawConnect is not reachable at {E2ETestSettings.BaseUrl}. Start the app and verify /health before running E2E tests.";
    }

    private static bool IsPawConnectAvailable()
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            using var response = client.GetAsync($"{E2ETestSettings.BaseUrl}/health").GetAwaiter().GetResult();
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch
        {
            return false;
        }
    }
}
