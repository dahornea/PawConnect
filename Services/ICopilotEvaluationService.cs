namespace PawConnect.Services;

public interface ICopilotEvaluationService
{
    Task<IReadOnlyList<CopilotEvaluationCase>> GetCasesAsync(CancellationToken cancellationToken = default);

    Task<CopilotEvaluationResult> RunCaseAsync(
        CopilotEvaluationCase evaluationCase,
        string evaluatorUserId,
        double passThresholdPercent = 70,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CopilotEvaluationResult>> RunAllAsync(
        string evaluatorUserId,
        double passThresholdPercent = 70,
        CancellationToken cancellationToken = default);

    ExportFile BuildJsonExport(IReadOnlyList<CopilotEvaluationResult> results);
}
