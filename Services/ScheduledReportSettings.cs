namespace PawConnect.Services;

public class ScheduledReportSettings
{
    public bool Enabled { get; set; }

    public bool RunOnStartupInDevelopment { get; set; }

    public int ShelterReportIntervalMinutes { get; set; } = 60;

    public int GetSafeShelterReportIntervalMinutes()
    {
        return ShelterReportIntervalMinutes > 0 ? ShelterReportIntervalMinutes : 60;
    }
}
