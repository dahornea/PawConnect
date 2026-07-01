namespace PawConnect.Services;

public class VisitReminderSettings
{
    public bool Enabled { get; set; }

    public int CheckIntervalMinutes { get; set; } = 60;

    public int ReminderHoursBeforeVisit { get; set; } = 24;

    public int GetSafeCheckIntervalMinutes()
    {
        return CheckIntervalMinutes > 0 ? CheckIntervalMinutes : 60;
    }

    public int GetSafeReminderHoursBeforeVisit()
    {
        return ReminderHoursBeforeVisit > 0 ? ReminderHoursBeforeVisit : 24;
    }

    public TimeSpan GetReminderWindow()
    {
        return TimeSpan.FromHours(1);
    }
}
