using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class AuditLogService(
    ApplicationDbContext context,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLogService> logger) : IAuditLogService
{
    public async Task LogAsync(AuditLog log)
    {
        try
        {
            NormalizeLog(log);
            await EnrichUserAsync(log);

            context.AuditLogs.Add(log);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit logging failed for action {Action}.", log.Action);
        }
    }

    public Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        string? userId = null,
        string? userEmail = null,
        string? userRole = null,
        string? additionalData = null)
    {
        return LogAsync(new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            UserId = userId,
            UserEmail = userEmail,
            UserRole = userRole,
            AdditionalData = additionalData,
            CreatedAt = DateTime.UtcNow
        });
    }

    public Task LogSystemAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        string? additionalData = null)
    {
        return LogAsync(action, entityName, entityId, description, userEmail: "System", userRole: "System", additionalData: additionalData);
    }

    public Task<List<AuditLog>> GetRecentLogsAsync(int count)
    {
        return context.AuditLogs
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(Math.Clamp(count, 1, 500))
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<AuditLog>> GetLogsAsync(
        string? action = null,
        string? entityName = null,
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int take = 200)
    {
        var query = context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(log => log.EntityName == entityName);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(log =>
                log.Description.Contains(term) ||
                (log.UserEmail != null && log.UserEmail.Contains(term)) ||
                (log.EntityId != null && log.EntityId.Contains(term)));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= toDate.Value);
        }

        return query
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(Math.Clamp(take, 1, 1000))
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<AuditLog>> GetLogsForEntityAsync(string entityName, string entityId)
    {
        return context.AuditLogs
            .Where(log => log.EntityName == entityName && log.EntityId == entityId)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .AsNoTracking()
            .ToListAsync();
    }

    private async Task EnrichUserAsync(AuditLog log)
    {
        var user = httpContextAccessor.HttpContext?.User;
        log.UserId ??= user?.FindFirstValue(ClaimTypes.NameIdentifier);
        log.UserEmail ??= user?.FindFirstValue(ClaimTypes.Email) ?? user?.Identity?.Name;
        log.UserRole ??= user?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).FirstOrDefault();
        log.IpAddress ??= httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrWhiteSpace(log.UserId))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(log.UserEmail))
        {
            log.UserEmail = await context.Users
                .Where(applicationUser => applicationUser.Id == log.UserId)
                .Select(applicationUser => applicationUser.Email)
                .FirstOrDefaultAsync();
        }

        if (string.IsNullOrWhiteSpace(log.UserRole))
        {
            log.UserRole = await (
                    from userRole in context.UserRoles
                    join role in context.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == log.UserId
                    select role.Name)
                .FirstOrDefaultAsync();
        }
    }

    private static void NormalizeLog(AuditLog log)
    {
        log.Action = NormalizeRequired(log.Action, 80);
        log.EntityName = NormalizeRequired(log.EntityName, 80);
        log.EntityId = NormalizeOptional(log.EntityId, 80);
        log.Description = NormalizeRequired(log.Description, 1000);
        log.UserId = NormalizeOptional(log.UserId, 450);
        log.UserEmail = NormalizeOptional(log.UserEmail, 256);
        log.UserRole = NormalizeOptional(log.UserRole, 80);
        log.IpAddress = NormalizeOptional(log.IpAddress, 64);
        log.AdditionalData = NormalizeOptional(log.AdditionalData, 2000);

        if (log.CreatedAt == default)
        {
            log.CreatedAt = DateTime.UtcNow;
        }
    }

    private static string NormalizeRequired(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
