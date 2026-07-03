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

    Task LogUserActionAsync(
        string action,
        string entityType,
        string? entityId,
        string summary,
        object? details = null,
        string severity = "Information",
        string eventType = "Business");

    Task LogSystemEventAsync(
        string action,
        string entityType,
        string? entityId,
        string summary,
        object? details = null,
        string severity = "Information");

    Task LogCopilotEventAsync(
        string action,
        string? entityId,
        string summary,
        object? details = null,
        string severity = "Information");

    Task<List<AuditLog>> GetRecentLogsAsync(int count);

    Task<List<AuditLog>> GetLogsAsync(
        string? action = null,
        string? entityName = null,
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? severity = null,
        string? eventType = null,
        string? correlationId = null,
        int take = 200);

    Task<List<AuditLog>> GetLogsForEntityAsync(string entityName, string entityId);
}
