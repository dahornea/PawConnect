using PawConnect.Entities;
using MudBlazor;

namespace PawConnect.Components.Shared;

public static class IntelligenceUi
{
    public static string SeverityLabel(IntelligenceSeverity severity) => severity switch
    {
        IntelligenceSeverity.Informational => "Info",
        _ => SplitWords(severity.ToString())
    };

    public static string CategoryLabel(IntelligenceCategory category) => category switch
    {
        IntelligenceCategory.Adoption => "Adoption",
        IntelligenceCategory.DogProfileQuality => "Dog profile",
        IntelligenceCategory.ApplicationReview => "Application review",
        IntelligenceCategory.Workload => "Workload",
        IntelligenceCategory.Notifications => "Notification delivery",
        IntelligenceCategory.Transfer => "Shelter transfer",
        IntelligenceCategory.Volunteer => "Volunteer task",
        IntelligenceCategory.PlatformHealth => "Platform reliability",
        IntelligenceCategory.Matching => "Dog matching",
        IntelligenceCategory.UserNextStep => "Next step",
        _ => SplitWords(category.ToString())
    };

    public static string SourceLabel(string sourceModule) => sourceModule switch
    {
        "DogOperations" => "Dog profile",
        "AdoptionReview" => "Application review",
        "DogTransfers" => "Shelter transfer",
        "VolunteerTasks" => "Volunteer task",
        "NotificationReliability" => "Notification delivery",
        "AdopterNextSteps" => "Adopter next step",
        _ => SplitWords(sourceModule)
    };

    public static string StatusLabel(IntelligenceInsightStatus status) => status switch
    {
        IntelligenceInsightStatus.Active => "Needs attention",
        _ => SplitWords(status.ToString())
    };

    public static string SeverityCss(IntelligenceSeverity severity) => $"severity-{severity.ToString().ToLowerInvariant()}";

    public static Color SeverityColor(IntelligenceSeverity severity) => severity switch
    {
        IntelligenceSeverity.Critical => Color.Error,
        IntelligenceSeverity.High => Color.Warning,
        IntelligenceSeverity.Medium => Color.Info,
        IntelligenceSeverity.Low => Color.Secondary,
        _ => Color.Success
    };

    public static string FormatLocalDateTime(DateTime value) => value.ToLocalTime().ToString("dd MMM yyyy, HH:mm");

    public static string FormatAge(DateTime value)
    {
        var elapsed = DateTime.UtcNow - value;
        if (elapsed.TotalMinutes < 1) return "Just now";
        if (elapsed.TotalHours < 1) return $"{Math.Max(1, (int)elapsed.TotalMinutes)}m ago";
        if (elapsed.TotalDays < 1) return $"{Math.Max(1, (int)elapsed.TotalHours)}h ago";
        if (elapsed.TotalDays < 30) return $"{Math.Max(1, (int)elapsed.TotalDays)}d ago";
        return value.ToLocalTime().ToString("dd MMM");
    }

    private static string SplitWords(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
}

public sealed record IntelligenceSnoozeRequest(int InsightId, TimeSpan Duration);
public sealed record IntelligenceResolveRequest(int InsightId, string Reason);
