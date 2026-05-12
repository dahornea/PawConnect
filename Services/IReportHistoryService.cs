using PawConnect.Entities;

namespace PawConnect.Services;

public interface IReportHistoryService
{
    Task RecordReportGeneratedAsync(ReportHistoryRecord record);

    Task RecordReportSentAsync(ReportHistoryRecord record);

    Task RecordReportFailedAsync(ReportHistoryRecord record);

    Task<List<ReportHistory>> GetReportHistoryForShelterAsync(int shelterId, int count = 100);

    Task<List<ReportHistory>> GetAdminReportHistoryAsync(
        string? reportType = null,
        bool? wasSuccessful = null,
        int take = 300);

    Task<List<ReportHistory>> GetRecentReportsAsync(int count);

    Task<List<ReportHistory>> GetReportHistoryByTypeAsync(string reportType, int count = 100);
}

public sealed record ReportHistoryRecord(
    string ReportType,
    string TriggeredBy,
    string? RecipientEmail = null,
    string? Subject = null,
    string? FileName = null,
    DateTime? GeneratedAt = null,
    DateTime? SentAt = null,
    string? ErrorMessage = null,
    string? TriggeredByUserId = null,
    string? TriggeredByUserEmail = null,
    int? ShelterId = null,
    string? AdminUserId = null,
    string? RelatedEntityName = null,
    string? RelatedEntityId = null);
