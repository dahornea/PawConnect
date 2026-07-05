using System.Diagnostics;

namespace PawConnect.IntegrationTests.Infrastructure;

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PAWCONNECT_SKIP_DOCKER_TESTS") == "1")
        {
            Skip = "Docker integration tests were skipped through PAWCONNECT_SKIP_DOCKER_TESTS=1.";
            return;
        }

        if (!IsDockerAvailable())
        {
            Skip = "Docker is not available or not running. Start Docker Desktop to run SQL Server integration tests.";
        }
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return false;
            }

            return process.WaitForExit(5000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
