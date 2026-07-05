using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class NotificationOutboxService(IDbContextFactory<ApplicationDbContext> contextFactory) : INotificationOutboxService
{
    public async Task<NotificationOutboxMessageDto> EnqueueAsync(
        NotificationOutboxCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var message = await EnqueueInternalAsync(context, request, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(message);
    }

    public async Task<IReadOnlyList<NotificationOutboxMessageDto>> EnqueueManyAsync(
        IEnumerable<NotificationOutboxCreateRequest> requests,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var messages = new List<NotificationOutboxMessage>();

        foreach (var request in requests)
        {
            messages.Add(await EnqueueInternalAsync(context, request, cancellationToken));
        }

        await context.SaveChangesAsync(cancellationToken);
        return messages.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<NotificationOutboxMessageDto>> GetAdminMessagesAsync(
        NotificationOutboxFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);

        var messages = await ApplyFilter(BuildQuery(context), filter)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(500)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return messages.Select(ToDto).ToList();
    }

    public async Task<NotificationOutboxSummaryDto> GetAdminSummaryAsync(
        NotificationOutboxFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);

        var query = ApplyFilter(context.NotificationOutboxMessages.AsNoTracking(), filter);
        return new NotificationOutboxSummaryDto(
            await query.CountAsync(cancellationToken),
            await query.CountAsync(message => message.Status == NotificationOutboxStatus.Pending, cancellationToken),
            await query.CountAsync(message => message.Status == NotificationOutboxStatus.Processing, cancellationToken),
            await query.CountAsync(message => message.Status == NotificationOutboxStatus.Sent, cancellationToken),
            await query.CountAsync(message => message.Status == NotificationOutboxStatus.Failed, cancellationToken),
            await query.CountAsync(message => message.Status == NotificationOutboxStatus.DeadLetter, cancellationToken),
            await query.CountAsync(message => message.Status == NotificationOutboxStatus.Cancelled, cancellationToken));
    }

    public async Task<NotificationOutboxMessageDto?> GetAdminMessageAsync(
        int outboxMessageId,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);

        var message = await BuildQuery(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(message => message.Id == outboxMessageId, cancellationToken);

        return message is null ? null : ToDto(message);
    }

    public async Task RetryAsync(
        int outboxMessageId,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);

        var message = await context.NotificationOutboxMessages
            .FirstOrDefaultAsync(message => message.Id == outboxMessageId, cancellationToken)
            ?? throw new InvalidOperationException("Outbox message was not found.");

        if (message.Status is NotificationOutboxStatus.Sent or NotificationOutboxStatus.Cancelled)
        {
            throw new InvalidOperationException("Only failed, dead-letter, pending, or processing outbox messages can be retried.");
        }

        var now = DateTime.UtcNow;
        message.Status = NotificationOutboxStatus.Pending;
        message.NextAttemptAt = now;
        message.LastError = null;
        message.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(
        int outboxMessageId,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);

        var message = await context.NotificationOutboxMessages
            .FirstOrDefaultAsync(message => message.Id == outboxMessageId, cancellationToken)
            ?? throw new InvalidOperationException("Outbox message was not found.");

        if (message.Status == NotificationOutboxStatus.Sent)
        {
            throw new InvalidOperationException("Sent outbox messages cannot be cancelled.");
        }

        message.Status = NotificationOutboxStatus.Cancelled;
        message.NextAttemptAt = null;
        message.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<NotificationOutboxMessage> EnqueueInternalAsync(
        ApplicationDbContext context,
        NotificationOutboxCreateRequest request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = NormalizeOptional(request.IdempotencyKey, 160);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await context.NotificationOutboxMessages
                .Include(message => message.RecipientUser)
                .FirstOrDefaultAsync(message => message.IdempotencyKey == idempotencyKey, cancellationToken);

            if (existing is not null)
            {
                return existing;
            }
        }

        var now = DateTime.UtcNow;
        var message = new NotificationOutboxMessage
        {
            RecipientUserId = NormalizeOptional(request.RecipientUserId, 450),
            RecipientEmail = NormalizeOptional(request.RecipientEmail, 256),
            NotificationType = request.NotificationType,
            Channel = request.Channel,
            Subject = NormalizeRequired(request.Subject, 120),
            Body = NormalizeRequired(request.Body, 2000),
            Link = NormalizeOptional(request.Link, 300),
            RelatedEntityType = NormalizeOptional(request.RelatedEntityType, 80),
            RelatedEntityId = NormalizeOptional(request.RelatedEntityId, 80),
            CorrelationId = NormalizeOptional(request.CorrelationId, 120),
            IdempotencyKey = idempotencyKey,
            MaxAttempts = Math.Clamp(request.MaxAttempts, 1, 10),
            NextAttemptAt = request.NextAttemptAt ?? now,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.NotificationOutboxMessages.Add(message);
        return message;
    }

    private static IQueryable<NotificationOutboxMessage> BuildQuery(ApplicationDbContext context)
    {
        return context.NotificationOutboxMessages.Include(message => message.RecipientUser);
    }

    private static IQueryable<NotificationOutboxMessage> ApplyFilter(
        IQueryable<NotificationOutboxMessage> query,
        NotificationOutboxFilter filter)
    {
        if (filter.Status is not null)
        {
            query = query.Where(message => message.Status == filter.Status);
        }

        if (filter.Channel is not null)
        {
            query = query.Where(message => message.Channel == filter.Channel);
        }

        if (filter.NotificationType is not null)
        {
            query = query.Where(message => message.NotificationType == filter.NotificationType);
        }

        if (filter.From is not null)
        {
            query = query.Where(message => message.CreatedAt >= filter.From.Value.Date);
        }

        if (filter.To is not null)
        {
            var inclusiveTo = filter.To.Value.Date.AddDays(1);
            query = query.Where(message => message.CreatedAt < inclusiveTo);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(message =>
                (message.RecipientEmail != null && message.RecipientEmail.Contains(search)) ||
                message.Subject.Contains(search) ||
                message.Body.Contains(search) ||
                (message.RecipientUser != null && message.RecipientUser.Email != null && message.RecipientUser.Email.Contains(search)) ||
                (message.RecipientUser != null && message.RecipientUser.FullName != null && message.RecipientUser.FullName.Contains(search)));
        }

        return query;
    }

    private static NotificationOutboxMessageDto ToDto(NotificationOutboxMessage message)
    {
        var displayName = message.RecipientUser is null
            ? "External recipient"
            : string.IsNullOrWhiteSpace(message.RecipientUser.FullName)
                ? message.RecipientUser.Email ?? message.RecipientUserId ?? "Unknown user"
                : message.RecipientUser.FullName;

        return new NotificationOutboxMessageDto(
            message.Id,
            message.RecipientUserId,
            displayName,
            MaskRecipient(message.RecipientEmail),
            message.NotificationType,
            message.Channel,
            message.Status,
            message.Subject,
            message.Body,
            message.Link,
            message.RelatedEntityType,
            message.RelatedEntityId,
            message.CorrelationId,
            message.IdempotencyKey,
            message.AttemptCount,
            message.MaxAttempts,
            message.NextAttemptAt,
            message.LastAttemptAt,
            message.SentAt,
            message.LastError,
            message.CreatedAt,
            message.UpdatedAt);
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
            throw new InvalidOperationException("Only administrators can manage notification outbox messages.");
        }
    }

    private static string NormalizeRequired(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Notification" : value.Trim();
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
