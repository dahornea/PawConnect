using PawConnect.Entities;

namespace PawConnect.Services;

public interface IMessageReportService
{
    IReadOnlyList<string> AllowedReasons { get; }

    Task<MessageReportDto> ReportMessageAsync(
        int messageId,
        string reason,
        string? details,
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageReportDto>> GetAdminReportsAsync(
        MessageReportFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default);

    Task<MessageReportDto> ReviewReportAsync(
        int reportId,
        MessageReportStatus status,
        string? adminNote,
        string adminUserId,
        CancellationToken cancellationToken = default);
}
