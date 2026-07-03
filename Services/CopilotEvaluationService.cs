using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace PawConnect.Services;

public class CopilotEvaluationService(
    IWebHostEnvironment environment,
    IAdoptionCopilotService adoptionCopilotService,
    ICopilotCriteriaComparisonService comparisonService,
    ILogger<CopilotEvaluationService> logger,
    IAuditLogService? auditLogService = null) : ICopilotEvaluationService
{
    private const string EvaluationCasesPath = "Data/CopilotEvaluation/copilot-evaluation-cases.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<IReadOnlyList<CopilotEvaluationCase>> GetCasesAsync(CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(environment.ContentRootPath, EvaluationCasesPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return BuildFallbackCases();
        }

        try
        {
            await using var stream = File.OpenRead(fullPath);
            var cases = await JsonSerializer.DeserializeAsync<List<CopilotEvaluationCase>>(stream, JsonOptions, cancellationToken);
            return cases?
                .Where(IsUsableCase)
                .ToList() ?? BuildFallbackCases();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Copilot evaluation cases could not be loaded from {Path}.", fullPath);
            return BuildFallbackCases();
        }
    }

    public async Task<CopilotEvaluationResult> RunCaseAsync(
        CopilotEvaluationCase evaluationCase,
        string evaluatorUserId,
        double passThresholdPercent = 70,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await adoptionCopilotService.AskAsync(evaluatorUserId, evaluationCase.Prompt, cancellationToken);
            stopwatch.Stop();

            var actualCriteria = response.AppliedConstraints ?? [];
            var expectedCriteria = evaluationCase.ExpectedCriteria
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)pair.Value,
                    StringComparer.OrdinalIgnoreCase);
            var comparison = comparisonService.Compare(expectedCriteria, actualCriteria);
            var expectedDogScore = CalculateExpectedDogMatchPercent(evaluationCase.ExpectedDogNames, response.Results);
            var error = string.Empty;
            if (comparison.ExpectedFieldCount > 0 && actualCriteria.Count == 0)
            {
                error = "No structured criteria were extracted.";
            }

            var passed = comparison.AccuracyPercent >= passThresholdPercent && string.IsNullOrWhiteSpace(error);
            var evaluationResult = new CopilotEvaluationResult(
                evaluationCase,
                comparison,
                actualCriteria,
                response.Results.Select(ToEvaluationDogResult).ToList(),
                response.UsedAiEnhancement,
                response.UsedSemanticSearch,
                response.UsedToolCalling,
                stopwatch.ElapsedMilliseconds,
                passed,
                string.IsNullOrWhiteSpace(error) ? null : error,
                expectedDogScore);

            await AuditEvaluationRunAsync(evaluationResult, passThresholdPercent, cancellationToken);
            return evaluationResult;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or HttpRequestException or TaskCanceledException)
        {
            stopwatch.Stop();
            var expectedCriteria = evaluationCase.ExpectedCriteria
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)pair.Value,
                    StringComparer.OrdinalIgnoreCase);
            var failedComparison = comparisonService.Compare(expectedCriteria, []);
            var failedResult = new CopilotEvaluationResult(
                evaluationCase,
                failedComparison,
                [],
                [],
                false,
                false,
                false,
                stopwatch.ElapsedMilliseconds,
                false,
                ex.Message);

            await AuditEvaluationRunAsync(failedResult, passThresholdPercent, cancellationToken);
            return failedResult;
        }
    }

    public async Task<IReadOnlyList<CopilotEvaluationResult>> RunAllAsync(
        string evaluatorUserId,
        double passThresholdPercent = 70,
        CancellationToken cancellationToken = default)
    {
        var cases = await GetCasesAsync(cancellationToken);
        var results = new List<CopilotEvaluationResult>();
        foreach (var evaluationCase in cases)
        {
            results.Add(await RunCaseAsync(evaluationCase, evaluatorUserId, passThresholdPercent, cancellationToken));
        }

        return results;
    }

    public ExportFile BuildJsonExport(IReadOnlyList<CopilotEvaluationResult> results)
    {
        var payload = results.Select(result => new
        {
            caseId = result.Case.Id,
            caseTitle = result.Case.Title,
            prompt = result.Case.Prompt,
            expectedCriteria = result.Case.ExpectedCriteria,
            actualCriteria = result.ActualCriteria,
            accuracyPercent = Math.Round(result.Comparison.AccuracyPercent, 1),
            passed = result.Passed,
            durationMs = result.DurationMs,
            expectedDogMatchPercent = result.ExpectedDogMatchPercent,
            recommendedDogs = result.RecommendedDogs,
            error = result.ErrorMessage
        });

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new ExportFile(
            $"copilot-evaluation-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json",
            "application/json",
            Encoding.UTF8.GetBytes(json));
    }

    private static bool IsUsableCase(CopilotEvaluationCase evaluationCase)
    {
        return !string.IsNullOrWhiteSpace(evaluationCase.Id) &&
               !string.IsNullOrWhiteSpace(evaluationCase.Title) &&
               !string.IsNullOrWhiteSpace(evaluationCase.Prompt);
    }

    private static CopilotEvaluationDogResult ToEvaluationDogResult(AdoptionCopilotDogResult result)
    {
        return new CopilotEvaluationDogResult(
            result.DogId,
            result.Dog.Name,
            result.ScorePercent,
            result.MatchLabel,
            result.DisplayTags ?? [],
            result.CautionTags ?? []);
    }

    private static double? CalculateExpectedDogMatchPercent(
        IReadOnlyList<string> expectedDogNames,
        IReadOnlyList<AdoptionCopilotDogResult> results)
    {
        if (expectedDogNames.Count == 0)
        {
            return null;
        }

        var actualNames = results
            .Take(6)
            .Select(result => result.Dog.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matched = expectedDogNames.Count(name => actualNames.Contains(name));
        return matched * 100.0 / expectedDogNames.Count;
    }

    private static IReadOnlyList<CopilotEvaluationCase> BuildFallbackCases()
    {
        return
        [
            new CopilotEvaluationCase
            {
                Id = "apartment-calm-small-children",
                Title = "Apartment calm small dog with children",
                Prompt = "I live in an apartment and want a calm small dog good with children.",
                ExpectedCriteria = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Home"] = ["Apartment"],
                    ["Size"] = ["Small"],
                    ["Temperament"] = ["Calm"],
                    ["Compatibility"] = ["Children"]
                },
                Notes = "Checks that the Copilot extracts home, size, temperament, and child compatibility."
            },
            new CopilotEvaluationCase
            {
                Id = "cat-friendly",
                Title = "Cat compatibility request",
                Prompt = "I have a cat at home and need a dog that can adjust safely.",
                ExpectedCriteria = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Compatibility"] = ["Cats"]
                },
                Notes = "Checks pet compatibility extraction without adding unrelated apartment criteria."
            }
        ];
    }

    private Task AuditEvaluationRunAsync(
        CopilotEvaluationResult result,
        double passThresholdPercent,
        CancellationToken cancellationToken)
    {
        var action = result.ErrorMessage is null
            ? AuditActions.CopilotEvaluationRun
            : AuditActions.CopilotEvaluationFailed;
        var severity = result.Passed ? "Information" : "Warning";

        logger.LogInformation(
            "Copilot evaluation case {CaseId} completed with accuracy {AccuracyPercent}% in {DurationMs} ms.",
            result.Case.Id,
            Math.Round(result.Comparison.AccuracyPercent, 1),
            result.DurationMs);

        return auditLogService?.LogSystemEventAsync(
            action,
            "CopilotEvaluation",
            result.Case.Id,
            $"Copilot evaluation case '{result.Case.Title}' completed.",
            new
            {
                CaseId = result.Case.Id,
                result.Case.Title,
                PromptSummary = Truncate(result.Case.Prompt, 240),
                AccuracyPercent = Math.Round(result.Comparison.AccuracyPercent, 1),
                result.Passed,
                PassThresholdPercent = passThresholdPercent,
                result.DurationMs,
                ExpectedFieldCount = result.Comparison.ExpectedFieldCount,
                CorrectFieldCount = result.Comparison.CorrectFieldCount,
                MissingFieldCount = result.Comparison.MissingFieldCount,
                ExtraFieldCount = result.Comparison.ExtraFieldCount,
                RecommendedDogCount = result.RecommendedDogs.Count,
                Error = Truncate(result.ErrorMessage, 300)
            },
            severity) ?? Task.CompletedTask;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
