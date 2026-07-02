using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminMessageReports
{
    private const int MaxAdminNoteLength = PawConnect.Services.MessageReportService.MaxAdminNoteLength;

    [Inject] private IMessageReportService MessageReportService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private static readonly IReadOnlyList<MessageReportStatus> StatusFilters = Enum.GetValues<MessageReportStatus>();

    private readonly Dictionary<int, string?> _adminNotes = [];
    private readonly HashSet<int> _reportsInReview = [];
    private List<MessageReportDto> _reports = [];
    private string? _statusFilter;
    private string? _reporterSearch;
    private string? _error;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadReportsAsync();
    }

    private async Task LoadReportsAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            var adminUserId = await GetCurrentUserIdAsync();
            _reports = (await MessageReportService.GetAdminReportsAsync(BuildFilter(), adminUserId)).ToList();
            _adminNotes.Clear();
            foreach (var report in _reports)
            {
                _adminNotes[report.Id] = report.AdminNote;
            }
        }
        catch (InvalidOperationException ex)
        {
            _error = ex.Message;
        }
        catch
        {
            _error = "Message reports could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ClearFiltersAsync()
    {
        _statusFilter = null;
        _reporterSearch = null;
        _fromDate = null;
        _toDate = null;
        await LoadReportsAsync();
    }

    private async Task ReviewAsync(MessageReportDto report, MessageReportStatus status)
    {
        if (!_reportsInReview.Add(report.Id))
        {
            return;
        }

        try
        {
            var adminUserId = await GetCurrentUserIdAsync();
            var updated = await MessageReportService.ReviewReportAsync(
                report.Id,
                status,
                GetAdminNote(report),
                adminUserId);

            var index = _reports.FindIndex(existing => existing.Id == updated.Id);
            if (index >= 0)
            {
                _reports[index] = updated;
            }

            _adminNotes[updated.Id] = updated.AdminNote;
            Snackbar.Add("Message report updated.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Message report could not be updated right now.", Severity.Error);
        }
        finally
        {
            _reportsInReview.Remove(report.Id);
        }
    }

    private MessageReportFilter BuildFilter()
    {
        return new MessageReportFilter(
            TryParseStatus(_statusFilter),
            _fromDate?.Date,
            _toDate?.Date,
            _reporterSearch);
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Current user could not be found.");
        }

        return userId;
    }

    private string? GetAdminNote(MessageReportDto report)
    {
        return _adminNotes.TryGetValue(report.Id, out var note) ? note : report.AdminNote;
    }

    private void SetAdminNote(int reportId, string value)
    {
        _adminNotes[reportId] = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private bool IsReviewing(int reportId)
    {
        return _reportsInReview.Contains(reportId);
    }

    private static MessageReportStatus? TryParseStatus(string? value)
    {
        return Enum.TryParse<MessageReportStatus>(value, out var status) && Enum.IsDefined(status)
            ? status
            : null;
    }

    private static string FormatStatus(MessageReportStatus status)
    {
        return status switch
        {
            MessageReportStatus.Pending => "Pending",
            MessageReportStatus.Reviewed => "Reviewed",
            MessageReportStatus.Dismissed => "Dismissed",
            MessageReportStatus.ActionTaken => "Action taken",
            _ => status.ToString()
        };
    }

    private static Color GetStatusColor(MessageReportStatus status)
    {
        return status switch
        {
            MessageReportStatus.Pending => Color.Warning,
            MessageReportStatus.Reviewed => Color.Info,
            MessageReportStatus.Dismissed => Color.Default,
            MessageReportStatus.ActionTaken => Color.Error,
            _ => Color.Default
        };
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToLocalTime().ToString("dd MMM yyyy HH:mm");
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
    }
}
