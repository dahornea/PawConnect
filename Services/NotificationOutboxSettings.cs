namespace PawConnect.Services;

public class NotificationOutboxSettings
{
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 20;

    public int GetSafePollIntervalSeconds()
    {
        return Math.Clamp(PollIntervalSeconds, 10, 3600);
    }

    public int GetSafeBatchSize()
    {
        return Math.Clamp(BatchSize, 1, 100);
    }
}
