using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class NotificationDeliveryLogService(IDbContextFactory<ApplicationDbContext> contextFactory) : INotificationDeliveryLogService
{
    public async Task LogDeliveryAsync(
        NotificationDeliveryLogCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var log = new NotificationDeliveryLog
        {
            NotificationId = request.NotificationId,
            UserId = NormalizeOptional(request.UserId, 450),
            NotificationType = request.NotificationType,
            Channel = request.Channel,
            Status = request.Status,
            Recipient = NormalizeOptional(request.Recipient, 256),
            Subject = NormalizeOptional(request.Subject, 200),
            ErrorMessage = NormalizeError(request.ErrorMessage),
            ProviderMessageId = NormalizeOptional(request.ProviderMessageId, 120),
            CreatedAt = now,
            SentAt = request.SentAt ?? (request.Status == NotificationDeliveryStatus.Sent ? now : null),
            FailedAt = request.FailedAt ?? (request.Status == NotificationDeliveryStatus.Failed ? now : null),
            RetryCount = Math.Max(0, request.RetryCount),
            RelatedEntityType = NormalizeOptional(request.RelatedEntityType, 80),
            RelatedEntityId = NormalizeOptional(request.RelatedEntityId, 80)
        };

        context.NotificationDeliveryLogs.Add(log);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationDeliveryLogDto>> GetAdminDeliveryLogsAsync(
        NotificationDeliveryLogFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);

        var logs = await ApplyFilter(BuildQuery(context), filter)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(500)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return logs.Select(ToDto).ToList();
    }

    public async Task<NotificationDeliverySummaryDto> GetAdminDeliverySummaryAsync(
        NotificationDeliveryLogFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);

        var logs = ApplyFilter(context.NotificationDeliveryLogs.AsNoTracking(), filter);
        return new NotificationDeliverySummaryDto(
            await logs.CountAsync(cancellationToken),
            await logs.CountAsync(log => log.Status == NotificationDeliveryStatus.Sent, cancellationToken),
            await logs.CountAsync(log => log.Status == NotificationDeliveryStatus.Failed, cancellationToken),
            await logs.CountAsync(log => log.Status == NotificationDeliveryStatus.Pending, cancellationToken),
            await logs.CountAsync(log => log.Status == NotificationDeliveryStatus.Skipped, cancellationToken),
            await logs.CountAsync(log => log.Status == NotificationDeliveryStatus.DisabledByPreference, cancellationToken));
    }

    private static IQueryable<NotificationDeliveryLog> BuildQuery(ApplicationDbContext context)
    {
        return context.NotificationDeliveryLogs
            .Include(log => log.User);
    }

    private static IQueryable<NotificationDeliveryLog> ApplyFilter(
        IQueryable<NotificationDeliveryLog> query,
        NotificationDeliveryLogFilter filter)
    {
        if (filter.Status is not null)
        {
            query = query.Where(log => log.Status == filter.Status);
        }

        if (filter.Channel is not null)
        {
            query = query.Where(log => log.Channel == filter.Channel);
        }

        if (filter.NotificationType is not null)
        {
            query = query.Where(log => log.NotificationType == filter.NotificationType);
        }

        if (filter.From is not null)
        {
            query = query.Where(log => log.CreatedAt >= filter.From.Value.Date);
        }

        if (filter.To is not null)
        {
            var inclusiveTo = filter.To.Value.Date.AddDays(1);
            query = query.Where(log => log.CreatedAt < inclusiveTo);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(log =>
                (log.Recipient != null && log.Recipient.Contains(search)) ||
                (log.Subject != null && log.Subject.Contains(search)) ||
                (log.User != null && log.User.Email != null && log.User.Email.Contains(search)) ||
                (log.User != null && log.User.FullName != null && log.User.FullName.Contains(search)));
        }

        return query;
    }

    private static NotificationDeliveryLogDto ToDto(NotificationDeliveryLog log)
    {
        var displayName = log.User is null
            ? "External recipient"
            : string.IsNullOrWhiteSpace(log.User.FullName)
                ? log.User.Email ?? log.UserId ?? "Unknown user"
                : log.User.FullName;

        return new NotificationDeliveryLogDto(
            log.Id,
            log.NotificationId,
            log.UserId,
            displayName,
            log.NotificationType,
            log.Channel,
            log.Status,
            MaskRecipient(log.Recipient),
            log.Subject,
            log.ErrorMessage,
            log.ProviderMessageId,
            log.CreatedAt,
            log.SentAt,
            log.FailedAt,
            log.RetryCount,
            log.RelatedEntityType,
            log.RelatedEntityId);
    }

    private static async Task EnsureAdminAsync(
        ApplicationDbContext context,
        string adminUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new InvalidOperationException("Current user could not be found.");
        }

        var isAdmin = await context.UserRoles
            .Join(
                context.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .AnyAsync(
                roleAssignment =>
                    roleAssignment.UserId == adminUserId &&
                    roleAssignment.Name == IdentitySeedData.AdminRole,
                cancellationToken);

        if (!isAdmin)
        {
            throw new InvalidOperationException("Only administrators can view notification delivery logs.");
        }
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

    private static string? NormalizeError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var firstLine = value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        return firstLine.Length <= 500 ? firstLine : firstLine[..500];
    }

    private static string? MaskRecipient(string? recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return null;
        }

        var normalized = recipient.Trim();
        var atIndex = normalized.IndexOf('@');
        if (atIndex <= 0 || atIndex == normalized.Length - 1)
        {
            return normalized.Length <= 2 ? "***" : $"{normalized[0]}***";
        }

        return $"{normalized[0]}***{normalized[atIndex..]}";
    }
}
