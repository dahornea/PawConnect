namespace PawConnect.Services;

public sealed class CopilotEvaluationCase
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public Dictionary<string, List<string>> ExpectedCriteria { get; set; } = [];

    public List<string> ExpectedDogNames { get; set; } = [];

    public string? Notes { get; set; }
}

public sealed record CopilotEvaluationResult(
    CopilotEvaluationCase Case,
    CopilotCriteriaComparisonResult Comparison,
    IReadOnlyList<AdoptionCopilotConstraint> ActualCriteria,
    IReadOnlyList<CopilotEvaluationDogResult> RecommendedDogs,
    bool UsedAiEnhancement,
    bool UsedSemanticSearch,
    bool UsedToolCalling,
    long DurationMs,
    bool Passed,
    string? ErrorMessage = null,
    double? ExpectedDogMatchPercent = null);

public sealed record CopilotEvaluationDogResult(
    int DogId,
    string DogName,
    int ScorePercent,
    string MatchLabel,
    IReadOnlyList<string> DisplayTags,
    IReadOnlyList<string> CautionTags);

public sealed record CopilotCriteriaComparisonResult(
    int ExpectedFieldCount,
    int CorrectFieldCount,
    int MissingFieldCount,
    int ExtraFieldCount,
    double AccuracyPercent,
    IReadOnlyList<CopilotCriteriaFieldComparison> Fields,
    IReadOnlyList<CopilotCriteriaFieldComparison> ExtraFields);

public sealed record CopilotCriteriaFieldComparison(
    string Field,
    IReadOnlyList<string> ExpectedValues,
    IReadOnlyList<string> ActualValues,
    bool IsCorrect,
    bool IsMissing);

public sealed record CopilotEvaluationSummary(
    int TotalCases,
    int PassedCases,
    int FailedCases,
    double AverageFieldAccuracy,
    double AverageExecutionMs);
