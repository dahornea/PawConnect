namespace PawConnect.E2ETests.Infrastructure;

public static class E2ETestSettings
{
    public const string RunEnvironmentVariable = "PAWCONNECT_RUN_E2E";
    public const string BaseUrlEnvironmentVariable = "PAWCONNECT_E2E_BASE_URL";

    public static bool ShouldRun => string.Equals(
        Environment.GetEnvironmentVariable(RunEnvironmentVariable),
        "1",
        StringComparison.OrdinalIgnoreCase);

    public static string BaseUrl => (Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable) ?? "https://localhost:7125")
        .TrimEnd('/');

    public static bool Headless => !string.Equals(
        Environment.GetEnvironmentVariable("PAWCONNECT_E2E_HEADLESS"),
        "0",
        StringComparison.OrdinalIgnoreCase);

    public static int TimeoutMs => int.TryParse(Environment.GetEnvironmentVariable("PAWCONNECT_E2E_TIMEOUT_MS"), out var timeoutMs)
        ? Math.Clamp(timeoutMs, 5_000, 60_000)
        : 15_000;
}
