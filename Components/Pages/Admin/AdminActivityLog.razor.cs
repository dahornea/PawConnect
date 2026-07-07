using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminActivityLog
{
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;
    [Inject] private IBrowserFileDownloadService FileDownloadService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private static readonly IReadOnlyList<string> EntityFilters =
    [
        "AdoptionCopilot",
        "AdoptionRequest",
        "ApplicationUser",
        "CopilotEvaluation",
        "Dog",
        "DogImage",
        "Export",
        "MedicalRecord",
        "ResourceStock",
        "Shelter",
        "ShelterAvailabilitySlot",
        "ShelterRegistrationRequest"
    ];

    private static readonly IReadOnlyList<string> SeverityFilters =
    [
        "Information",
        "Warning",
        "Error"
    ];

    private static readonly IReadOnlyList<string> EventTypeFilters =
    [
        "Business",
        "Copilot",
        "System"
    ];

    private List<AuditLog> _logs = [];
    private bool _isLoading = true;
    private bool _isExporting;
    private string? _search;
    private string? _actionFilter;
    private string? _entityFilter;
    private string? _severityFilter;
    private string? _eventTypeFilter;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private bool HasAuditFilters =>
        !string.IsNullOrWhiteSpace(_search) ||
        !string.IsNullOrWhiteSpace(_actionFilter) ||
        !string.IsNullOrWhiteSpace(_entityFilter) ||
        !string.IsNullOrWhiteSpace(_severityFilter) ||
        !string.IsNullOrWhiteSpace(_eventTypeFilter) ||
        _fromDate.HasValue ||
        _toDate.HasValue;
    private string AuditEmptyTitle => HasAuditFilters ? "No audit logs match these filters" : "No audit logs recorded yet";
    private string AuditEmptyMessage => HasAuditFilters
        ? "Try widening the date range or clearing action, entity, severity, and event type filters."
        : "Important user, system, and Copilot events will appear here as the platform is used.";
    private string? AuditEmptyPrimaryActionText => HasAuditFilters ? "Clear filters" : null;
    private IReadOnlyList<string> AuditEmptyTips => HasAuditFilters
        ? ["Filters are combined, so a narrow date range plus entity filter can hide valid events."]
        : ["Audit logs are created by key platform actions such as Copilot, exports, and workflow changes."];

    protected override async Task OnInitializedAsync()
    {
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        _isLoading = true;

        try
        {
            _logs = await AuditLogService.GetLogsAsync(
                _actionFilter,
                _entityFilter,
                _search,
                _fromDate?.Date,
                _toDate?.Date.AddDays(1).AddTicks(-1),
                _severityFilter,
                _eventTypeFilter,
                take: 300);
        }
        catch
        {
            Snackbar.Add("Audit logs could not be loaded right now.", Severity.Error);
            _logs = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ClearFiltersAsync()
    {
        _search = null;
        _actionFilter = null;
        _entityFilter = null;
        _severityFilter = null;
        _eventTypeFilter = null;
        _fromDate = null;
        _toDate = null;
        await LoadLogsAsync();
    }

    private async Task ExportCsvAsync()
    {
        _isExporting = true;

        try
        {
            var rows = new List<IReadOnlyList<string?>>
            {
                new string?[] { "TimestampUtc", "Severity", "EventType", "Action", "Entity", "EntityId", "Summary", "UserEmail", "UserRole", "CorrelationId" }
            };

            rows.AddRange(_logs.Select(log => new[]
            {
                log.CreatedAt.ToString("O"),
                log.Severity,
                log.EventType,
                log.Action,
                log.EntityName,
                log.EntityId,
                log.Description,
                log.UserEmail,
                log.UserRole,
                log.CorrelationId
            }));

            var file = new ExportFile(
                $"pawconnect-audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv",
                "text/csv;charset=utf-8",
                Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BuildCsv(rows))).ToArray());

            await FileDownloadService.DownloadAsync(file);
            Snackbar.Add("Audit log export generated.", Severity.Success);
        }
        catch
        {
            Snackbar.Add("Could not export audit logs right now.", Severity.Error);
        }
        finally
        {
            _isExporting = false;
        }
    }

    private static Color GetActionColor(string action)
    {
        if (action.Contains("Deleted", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Rejected", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Cancelled", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Failed", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Error;
        }

        if (action.Contains("Created", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Accepted", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Submitted", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Success;
        }

        if (action.Contains("Report", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Export", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Copilot", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Info;
        }

        return Color.Primary;
    }

    private static Color GetSeverityColor(string severity)
    {
        return severity switch
        {
            "Error" => Color.Error,
            "Warning" => Color.Warning,
            _ => Color.Info
        };
    }

    private static string? ShortenCorrelationId(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return null;
        }

        return correlationId.Length <= 12 ? correlationId : $"{correlationId[..12]}...";
    }

    private static string FormatJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "-";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string BuildCsv(IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        return escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r')
            ? $"\"{escaped}\""
            : escaped;
    }
}
