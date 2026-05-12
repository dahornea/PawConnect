using PawConnect.Entities;

namespace PawConnect.Services;

public interface IAuditLogService
{
    Task LogAsync(AuditLog log);

    Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        string? userId = null,
        string? userEmail = null,
        string? userRole = null,
        string? additionalData = null);

    Task LogSystemAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        string? additionalData = null);

    Task<List<AuditLog>> GetRecentLogsAsync(int count);

    Task<List<AuditLog>> GetLogsAsync(
        string? action = null,
        string? entityName = null,
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int take = 200);

    Task<List<AuditLog>> GetLogsForEntityAsync(string entityName, string entityId);
}
