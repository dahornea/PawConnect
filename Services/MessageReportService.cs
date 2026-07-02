using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class MessageReportService(IDbContextFactory<ApplicationDbContext> contextFactory) : IMessageReportService
{
    public const int MaxDetailsLength = 1000;
    public const int MaxAdminNoteLength = 1000;

    private static readonly string[] ReportReasons =
    [
        "Inappropriate language",
        "Harassment or pressure",
        "Spam or scam",
        "Privacy concern",
        "Other"
    ];

    public IReadOnlyList<string> AllowedReasons => ReportReasons;

    public async Task<MessageReportDto> ReportMessageAsync(
        int messageId,
        string reason,
        string? details,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedReason = NormalizeReason(reason);
        var normalizedDetails = NormalizeOptionalText(details, MaxDetailsLength, "Details");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var message = await LoadMessageForReportAsync(context, messageId, cancellationToken);
        var request = message?.Conversation?.AdoptionRequest;
        ConversationService.EnsureCanAccessRequest(request, currentUserId);

        if (message!.SenderUserId == currentUserId)
        {
            throw new InvalidOperationException("You cannot report your own message.");
        }

        var alreadyReported = await context.MessageReports.AnyAsync(
            report => report.MessageId == messageId && report.ReporterUserId == currentUserId,
            cancellationToken);

        if (alreadyReported)
        {
            throw new InvalidOperationException("You have already reported this message.");
        }

        var report = new MessageReport
        {
            MessageId = messageId,
            ReporterUserId = currentUserId,
            Reason = normalizedReason,
            Details = normalizedDetails,
            Status = MessageReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        context.MessageReports.Add(report);
        await context.SaveChangesAsync(cancellationToken);

        var savedReport = await LoadReportForDtoAsync(context, report.Id, cancellationToken);
        return ToDto(savedReport!);
    }

    public async Task<IReadOnlyList<MessageReportDto>> GetAdminReportsAsync(
        MessageReportFilter filter,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);

        var query = BuildReportQuery(context);

        if (filter.Status is not null)
        {
            query = query.Where(report => report.Status == filter.Status);
        }

        if (filter.From is not null)
        {
            query = query.Where(report => report.CreatedAt >= filter.From.Value);
        }

        if (filter.To is not null)
        {
            var inclusiveTo = filter.To.Value.Date.AddDays(1);
            query = query.Where(report => report.CreatedAt < inclusiveTo);
        }

        if (!string.IsNullOrWhiteSpace(filter.ReporterSearch))
        {
            var search = filter.ReporterSearch.Trim();
            query = query.Where(report =>
                (report.ReporterUser!.FullName != null && report.ReporterUser.FullName.Contains(search)) ||
                (report.ReporterUser.Email != null && report.ReporterUser.Email.Contains(search)));
        }

        var reports = await query
            .OrderBy(report => report.Status == MessageReportStatus.Pending ? 0 : 1)
            .ThenByDescending(report => report.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return reports.Select(ToDto).ToList();
    }

    public async Task<MessageReportDto> ReviewReportAsync(
        int reportId,
        MessageReportStatus status,
        string? adminNote,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
        {
            throw new InvalidOperationException("Unsupported report status.");
        }

        var normalizedAdminNote = NormalizeOptionalText(adminNote, MaxAdminNoteLength, "Admin note");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureAdminAsync(context, adminUserId, cancellationToken);

        var report = await context.MessageReports
            .FirstOrDefaultAsync(existing => existing.Id == reportId, cancellationToken);

        if (report is null)
        {
            throw new InvalidOperationException("Message report could not be found.");
        }

        report.Status = status;
        report.AdminNote = normalizedAdminNote;
        report.ReviewedAt = DateTime.UtcNow;
        report.ReviewedByAdminId = adminUserId;
        await context.SaveChangesAsync(cancellationToken);

        var updatedReport = await LoadReportForDtoAsync(context, reportId, cancellationToken);
        return ToDto(updatedReport!);
    }

    private static IQueryable<MessageReport> BuildReportQuery(ApplicationDbContext context)
    {
        return context.MessageReports
            .Include(report => report.ReporterUser)
            .Include(report => report.ReviewedByAdmin)
            .Include(report => report.Message)
            .ThenInclude(message => message!.SenderUser)
            .Include(report => report.Message)
            .ThenInclude(message => message!.Conversation)
            .ThenInclude(conversation => conversation!.AdoptionRequest)
            .ThenInclude(request => request!.Adopter)
            .Include(report => report.Message)
            .ThenInclude(message => message!.Conversation)
            .ThenInclude(conversation => conversation!.AdoptionRequest)
            .ThenInclude(request => request!.Dog)
            .ThenInclude(dog => dog!.Shelter);
    }

    private static async Task<Message?> LoadMessageForReportAsync(
        ApplicationDbContext context,
        int messageId,
        CancellationToken cancellationToken)
    {
        return await context.Messages
            .Include(message => message.SenderUser)
            .Include(message => message.Conversation)
            .ThenInclude(conversation => conversation!.AdoptionRequest)
            .ThenInclude(request => request!.Adopter)
            .Include(message => message.Conversation)
            .ThenInclude(conversation => conversation!.AdoptionRequest)
            .ThenInclude(request => request!.Dog)
            .ThenInclude(dog => dog!.Shelter)
            .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
    }

    private static async Task<MessageReport?> LoadReportForDtoAsync(
        ApplicationDbContext context,
        int reportId,
        CancellationToken cancellationToken)
    {
        return await BuildReportQuery(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(report => report.Id == reportId, cancellationToken);
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
            throw new InvalidOperationException("Only administrators can review message reports.");
        }
    }

    private static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Report reason is required.");
        }

        var normalized = reason.Trim();
        if (!ReportReasons.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Please select a valid report reason.");
        }

        return ReportReasons.First(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} must be {maxLength} characters or fewer.");
        }

        return normalized;
    }

    private static MessageReportDto ToDto(MessageReport report)
    {
        var message = report.Message ?? new Message();
        var request = message.Conversation?.AdoptionRequest;
        var senderIsAdopter = request?.AdopterId == message.SenderUserId;

        return new MessageReportDto(
            report.Id,
            report.MessageId,
            message.Body,
            senderIsAdopter
                ? ConversationService.GetAdopterDisplayName(request!)
                : ConversationService.GetShelterDisplayName(request!),
            senderIsAdopter ? "Adopter" : "Shelter",
            DisplayName(report.ReporterUser?.FullName, report.ReporterUser?.Email, "Unknown reporter"),
            report.ReporterUser?.Email ?? string.Empty,
            report.Reason,
            report.Details,
            report.Status,
            report.CreatedAt,
            report.ReviewedAt,
            report.ReviewedByAdmin is null
                ? null
                : DisplayName(report.ReviewedByAdmin.FullName, report.ReviewedByAdmin.Email, "Admin"),
            report.AdminNote,
            request?.Id ?? 0,
            string.IsNullOrWhiteSpace(request?.Dog?.Name) ? "Unknown dog" : request.Dog.Name,
            ConversationService.GetShelterDisplayName(request!),
            ConversationService.GetAdopterDisplayName(request!));
    }

    private static string DisplayName(string? fullName, string? email, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName.Trim();
        }

        return string.IsNullOrWhiteSpace(email) ? fallback : email.Trim();
    }
}
