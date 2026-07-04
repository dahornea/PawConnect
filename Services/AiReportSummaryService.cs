using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace PawConnect.Services;

public partial class AiReportSummaryService(
    IOpenAiReportSummaryClient openAiClient,
    IOptions<OpenAiSettings> options,
    ILogger<AiReportSummaryService> logger) : IAiReportSummaryService
{
    private const int MaxExecutiveSummaryLength = 800;
    private const int MaxItemLength = 240;
    private const int MaxHighlights = 5;
    private const int MaxWarnings = 5;
    private const int MaxSuggestedActions = 5;
    private const int MaxLimitations = 3;

    public async Task<AiReportSummaryResult> GenerateShelterSummaryAsync(
        ShelterReportSummaryMetricsDto metrics,
        CancellationToken cancellationToken = default)
    {
        var fallback = BuildShelterFallback(metrics, null);
        var settings = options.Value;
        if (!settings.Enabled || !settings.ReportSummariesEnabled || !settings.HasApiKey)
        {
            return fallback with { FallbackReason = "OpenAI report summaries are disabled or not configured." };
        }

        try
        {
            var request = new AiReportSummaryRequest(ReportHistoryTypes.ShelterSummaryReport, metrics);
            var response = await openAiClient.GenerateSummaryAsync(request, cancellationToken);
            if (!response.Success || response.Summary is null)
            {
                return fallback with { FallbackReason = response.ErrorMessage ?? "OpenAI report summary was unavailable." };
            }

            var normalized = Normalize(response.Summary);
            if (normalized is null)
            {
                return fallback with { FallbackReason = "OpenAI report summary response was empty or invalid." };
            }

            if (ContainsUnsupportedNumbers(normalized, metrics))
            {
                return fallback with { FallbackReason = "OpenAI report summary mentioned unsupported numbers." };
            }

            return normalized with { UsedAi = true, FallbackReason = null };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(ex, "AI report summary generation failed. Using deterministic fallback.");
            return fallback with { FallbackReason = "OpenAI report summary failed." };
        }
    }

    public static AiReportSummaryResult BuildShelterFallback(ShelterReportSummaryMetricsDto metrics, string? fallbackReason)
    {
        var lowStockText = metrics.LowStockResourceCount == 0
            ? "No resources are currently below their low-stock threshold."
            : $"{metrics.LowStockResourceCount} resource(s) are at or below the low-stock threshold.";

        var summary = $"During the selected period, {metrics.ShelterName} had {metrics.TotalDogs} dogs, including " +
            $"{metrics.AvailableDogs} available and {metrics.ReservedDogs} reserved. The shelter received " +
            $"{metrics.NewRequestsInPeriod} new adoption request(s), confirmed {metrics.ConfirmedVisitsInPeriod} visit(s), " +
            $"and recorded {metrics.RecentlyAdoptedDogs} recent adoption(s). {lowStockText}";

        var highlights = new List<string>
        {
            $"{metrics.AvailableDogs} available dog(s) and {metrics.ReservedDogs} reserved dog(s).",
            $"{metrics.NewRequestsInPeriod} new adoption request(s) in the report period.",
            $"{metrics.ConfirmedVisitsInPeriod} confirmed visit(s) in the report period."
        };

        var warnings = metrics.LowStockResourceCount == 0
            ? new List<string>()
            : [$"{metrics.LowStockResourceCount} low-stock resource(s) need review."];

        var actions = new List<string>
        {
            metrics.LowStockResourceCount > 0
                ? "Review low-stock resources and update inventory after restocking."
                : "Keep monitoring stock levels and adoption request progress."
        };

        return new AiReportSummaryResult(
            "Shelter Report Summary",
            summary,
            highlights,
            warnings,
            actions,
            ["This summary is generated only from PawConnect report metrics."],
            UsedAi: false,
            fallbackReason);
    }

    private static AiReportSummaryResult? Normalize(AiReportSummaryResult summary)
    {
        var executiveSummary = SafeTrim(summary.ExecutiveSummary, MaxExecutiveSummaryLength);
        if (string.IsNullOrWhiteSpace(executiveSummary))
        {
            return null;
        }

        return new AiReportSummaryResult(
            SafeTrim(summary.Title, 120) ?? "Report Summary",
            executiveSummary,
            NormalizeItems(summary.KeyHighlights, MaxHighlights),
            NormalizeItems(summary.Warnings, MaxWarnings),
            NormalizeItems(summary.SuggestedActions, MaxSuggestedActions),
            NormalizeItems(summary.Limitations, MaxLimitations),
            summary.UsedAi,
            summary.FallbackReason);
    }

    private static IReadOnlyList<string> NormalizeItems(IEnumerable<string?> values, int take)
    {
        return values
            .Select(value => SafeTrim(value, MaxItemLength))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    private static string? SafeTrim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var clean = value.Trim();
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    private static bool ContainsUnsupportedNumbers(AiReportSummaryResult summary, ShelterReportSummaryMetricsDto metrics)
    {
        var allowedNumbers = BuildAllowedNumbers(metrics);
        var text = string.Join(" ", new[]
        {
            summary.Title,
            summary.ExecutiveSummary,
            string.Join(" ", summary.KeyHighlights),
            string.Join(" ", summary.Warnings),
            string.Join(" ", summary.SuggestedActions),
            string.Join(" ", summary.Limitations)
        });

        foreach (Match match in IntegerRegex().Matches(text))
        {
            if (int.TryParse(match.Value, out var number) && !allowedNumbers.Contains(number))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<int> BuildAllowedNumbers(ShelterReportSummaryMetricsDto metrics)
    {
        var numbers = new HashSet<int>
        {
            metrics.FromDate.Year,
            metrics.FromDate.Month,
            metrics.FromDate.Day,
            metrics.ToDate.Year,
            metrics.ToDate.Month,
            metrics.ToDate.Day,
            metrics.TotalDogs,
            metrics.AvailableDogs,
            metrics.ReservedDogs,
            metrics.AdoptedDogs,
            metrics.InTreatmentDogs,
            metrics.NewRequestsInPeriod,
            metrics.PendingRequests,
            metrics.ConfirmedVisitsInPeriod,
            metrics.AcceptedRequests,
            metrics.RejectedRequests,
            metrics.CancelledRequests,
            metrics.TotalRequests,
            metrics.RecentlyAdoptedDogs,
            metrics.LowStockResourceCount
        };

        if (metrics.AverageDecisionDays.HasValue)
        {
            numbers.Add((int)Math.Round(metrics.AverageDecisionDays.Value));
        }

        foreach (var resource in metrics.CriticalLowStockResources)
        {
            numbers.Add(resource.Quantity);
            numbers.Add(resource.LowStockThreshold);
        }

        return numbers;
    }

    [GeneratedRegex(@"\b\d+\b", RegexOptions.Compiled)]
    private static partial Regex IntegerRegex();
}
