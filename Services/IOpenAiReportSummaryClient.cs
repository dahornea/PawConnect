namespace PawConnect.Services;

public interface IOpenAiReportSummaryClient
{
    Task<OpenAiReportSummaryResponse> GenerateSummaryAsync(
        AiReportSummaryRequest request,
        CancellationToken cancellationToken = default);
}
