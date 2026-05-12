using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ReportHistoryService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ReportHistoryService> logger) : IReportHistoryService
{
    public Task RecordReportGeneratedAsync(ReportHistoryRecord record)
    {
        return SaveHistoryAsync(record, wasSuccessful: true, sentAt: record.SentAt);
    }

    public Task RecordReportSentAsync(ReportHistoryRecord record)
    {
        return SaveHistoryAsync(record, wasSuccessful: true, sentAt: record.SentAt ?? DateTime.UtcNow);
    }

    public Task RecordReportFailedAsync(ReportHistoryRecord record)
    {
        return SaveHistoryAsync(record, wasSuccessful: false, sentAt: record.SentAt);
    }

    public async Task<List<ReportHistory>> GetReportHistoryForShelterAsync(int shelterId, int count = 100)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.ReportHistories
            .Where(history => history.ShelterId == shelterId)
            .OrderByDescending(history => history.GeneratedAt)
            .ThenByDescending(history => history.Id)
            .Take(Math.Clamp(count, 1, 500))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<ReportHistory>> GetAdminReportHistoryAsync(
        string? reportType = null,
        bool? wasSuccessful = null,
        int take = 300)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var query = context.ReportHistories
            .Include(history => history.Shelter)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(reportType))
        {
            query = query.Where(history => history.ReportType == reportType);
        }

        if (wasSuccessful.HasValue)
        {
            query = query.Where(history => history.WasSuccessful == wasSuccessful.Value);
        }

        return await query
            .OrderByDescending(history => history.GeneratedAt)
            .ThenByDescending(history => history.Id)
            .Take(Math.Clamp(take, 1, 1000))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<ReportHistory>> GetRecentReportsAsync(int count)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.ReportHistories
            .Include(history => history.Shelter)
            .OrderByDescending(history => history.GeneratedAt)
            .ThenByDescending(history => history.Id)
            .Take(Math.Clamp(count, 1, 500))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<ReportHistory>> GetReportHistoryByTypeAsync(string reportType, int count = 100)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.ReportHistories
            .Where(history => history.ReportType == reportType)
            .OrderByDescending(history => history.GeneratedAt)
            .ThenByDescending(history => history.Id)
            .Take(Math.Clamp(count, 1, 500))
            .AsNoTracking()
            .ToListAsync();
    }

    private async Task SaveHistoryAsync(ReportHistoryRecord record, bool wasSuccessful, DateTime? sentAt)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var history = BuildHistory(record, wasSuccessful, sentAt);
            await EnrichUserAsync(context, history);

            context.ReportHistories.Add(history);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Report history logging failed for report type {ReportType}.", record.ReportType);
        }
    }

    private async Task EnrichUserAsync(ApplicationDbContext context, ReportHistory history)
    {
        var user = httpContextAccessor.HttpContext?.User;
        history.TriggeredByUserId ??= user?.FindFirstValue(ClaimTypes.NameIdentifier);
        history.TriggeredByUserEmail ??= user?.FindFirstValue(ClaimTypes.Email) ?? user?.Identity?.Name;

        if (string.IsNullOrWhiteSpace(history.TriggeredByUserId) ||
            !string.IsNullOrWhiteSpace(history.TriggeredByUserEmail))
        {
            return;
        }

        history.TriggeredByUserEmail = await context.Users
            .Where(applicationUser => applicationUser.Id == history.TriggeredByUserId)
            .Select(applicationUser => applicationUser.Email)
            .FirstOrDefaultAsync();
    }

    private static ReportHistory BuildHistory(ReportHistoryRecord record, bool wasSuccessful, DateTime? sentAt)
    {
        return new ReportHistory
        {
            ReportType = NormalizeRequired(record.ReportType, 80),
            RecipientEmail = NormalizeOptional(record.RecipientEmail, 256),
            Subject = NormalizeOptional(record.Subject, 200),
            FileName = NormalizeOptional(record.FileName, 180),
            GeneratedAt = record.GeneratedAt ?? DateTime.UtcNow,
            SentAt = sentAt,
            WasSuccessful = wasSuccessful,
            ErrorMessage = wasSuccessful ? null : NormalizeOptional(record.ErrorMessage, 500),
            TriggeredBy = NormalizeRequired(record.TriggeredBy, 40),
            TriggeredByUserId = NormalizeOptional(record.TriggeredByUserId, 450),
            TriggeredByUserEmail = NormalizeOptional(record.TriggeredByUserEmail, 256),
            ShelterId = record.ShelterId,
            AdminUserId = NormalizeOptional(record.AdminUserId, 450),
            RelatedEntityName = NormalizeOptional(record.RelatedEntityName, 80),
            RelatedEntityId = NormalizeOptional(record.RelatedEntityId, 80)
        };
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
