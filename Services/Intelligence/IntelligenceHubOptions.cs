namespace PawConnect.Services.Intelligence;

public sealed class IntelligenceHubOptions
{
    public bool Enabled { get; set; } = true;
    public int ApplicationReviewWarningHours { get; set; } = 48;
    public int ApplicationReviewCriticalHours { get; set; } = 120;
    public int DogNoApplicationWarningDays { get; set; } = 21;
    public int DogNoApplicationCriticalDays { get; set; } = 45;
    public int LowProfileCompletenessThreshold { get; set; } = 70;
    public int CriticalProfileCompletenessThreshold { get; set; } = 45;
    public int VolunteerTaskOverdueWarningHours { get; set; } = 12;
    public int TransferPendingWarningHours { get; set; } = 48;
    public int OutboxFailedWarningCount { get; set; } = 3;
    public int OutboxDeadLetterCriticalCount { get; set; } = 1;
    public int InsightRefreshMinutes { get; set; } = 15;
    public int ManualRefreshCooldownSeconds { get; set; } = 30;
    public int MaximumInsightsPerDashboard { get; set; } = 50;
    public int HighPriorityNotificationThreshold { get; set; } = 80;

    public int GetSafeRefreshMinutes() => Math.Clamp(InsightRefreshMinutes, 5, 1440);
    public int GetSafeDashboardLimit() => Math.Clamp(MaximumInsightsPerDashboard, 5, 100);
}

