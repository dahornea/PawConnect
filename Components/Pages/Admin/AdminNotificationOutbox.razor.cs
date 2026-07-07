using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminNotificationOutbox
{
    private static readonly JsonSerializerOptions SavedViewJsonOptions = new(JsonSerializerDefaults.Web);

    [SupplyParameterFromQuery(Name = "savedViewId")]
    public int? SavedViewId { get; set; }

    [Inject] private INotificationOutboxService OutboxService { get; set; } = default!;
    [Inject] private INotificationOutboxProcessor OutboxProcessor { get; set; } = default!;
    [Inject] private IBulkNotificationOutboxActionService BulkOutboxActionService { get; set; } = default!;
    [Inject] private INotificationPreferenceService PreferenceService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private readonly IReadOnlyList<NotificationOutboxStatus> _statuses = Enum.GetValues<NotificationOutboxStatus>();
    private readonly IReadOnlyList<NotificationChannel> _channels = Enum.GetValues<NotificationChannel>();
    private IReadOnlyList<NotificationTypeDescriptionDto> _types = [];
    private List<NotificationOutboxMessageDto> _messages = [];
    private NotificationOutboxSummaryDto _summary = new(0, 0, 0, 0, 0, 0, 0);
    private NotificationOutboxMessageDto? _selected;
    private string? _statusFilter;
    private string? _channelFilter;
    private string? _typeFilter;
    private string? _search;
    private string? _error;
    private bool _isLoading = true;
    private bool _isProcessingNow;
    private bool _isBulkActionRunning;
    private BulkActionResultDto? _bulkActionResult;
    private readonly HashSet<int> _selectedOutboxMessageIds = [];
    private bool HasOutboxFilters =>
        !string.IsNullOrWhiteSpace(_statusFilter) ||
        !string.IsNullOrWhiteSpace(_channelFilter) ||
        !string.IsNullOrWhiteSpace(_typeFilter) ||
        !string.IsNullOrWhiteSpace(_search);
    private string OutboxEmptyTitle => HasOutboxFilters ? "No outbox messages match these filters" : "Notification queue is clear";
    private string OutboxEmptyMessage => HasOutboxFilters
        ? "Clear one or more filters to inspect the full notification outbox."
        : "Queued, retrying, and failed notification delivery work will appear here when there is something to process.";
    private string OutboxEmptyPrimaryActionText => HasOutboxFilters ? "Clear filters" : "Refresh";
    private string OutboxEmptyPrimaryActionIcon => HasOutboxFilters ? Icons.Material.Filled.FilterAltOff : Icons.Material.Filled.Refresh;
    private IReadOnlyList<string> OutboxEmptyTips => HasOutboxFilters
        ? ["Status, channel, type, and search filters are all applied together."]
        : ["A clear queue is normal when notification jobs are caught up.", "Use Process due now only when pending work exists."];
    private string OutboxSavedViewFilterStateJson => JsonSerializer.Serialize(
        new OutboxSavedViewState(_statusFilter, _channelFilter, _typeFilter, _search),
        SavedViewJsonOptions);
    private IReadOnlyList<string> OutboxSavedViewSummaryLabels => BuildOutboxSavedViewSummaryLabels();
    private IReadOnlyList<int> VisibleSelectedOutboxIds => _messages
        .Where(message => _selectedOutboxMessageIds.Contains(message.Id))
        .Select(message => message.Id)
        .ToList();
    private bool AreAllVisibleOutboxMessagesSelected => _messages.Count > 0 &&
        _messages.All(message => _selectedOutboxMessageIds.Contains(message.Id));

    protected override async Task OnInitializedAsync()
    {
        _types = PreferenceService.GetNotificationTypes();
        await LoadMessagesAsync();
    }

    private async Task LoadMessagesAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            var adminUserId = await GetCurrentUserIdAsync();
            var filter = BuildFilter();
            _messages = (await OutboxService.GetAdminMessagesAsync(filter, adminUserId)).ToList();
            _summary = await OutboxService.GetAdminSummaryAsync(filter, adminUserId);
            RemoveHiddenOutboxSelections();
        }
        catch (InvalidOperationException ex)
        {
            _error = ex.Message;
        }
        catch
        {
            _error = "Notification outbox messages could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ClearFiltersAsync()
    {
        _statusFilter = null;
        _channelFilter = null;
        _typeFilter = null;
        _search = null;
        ClearOutboxSelection();
        await LoadMessagesAsync();
    }

    private async Task ApplySavedViewAsync(SavedViewDto savedView)
    {
        try
        {
            var state = JsonSerializer.Deserialize<OutboxSavedViewState>(savedView.FilterStateJson, SavedViewJsonOptions);
            _statusFilter = state?.Status;
            _channelFilter = state?.Channel;
            _typeFilter = state?.Type;
            _search = state?.Search;
            ClearOutboxSelection();
            await LoadMessagesAsync();
        }
        catch (JsonException)
        {
            Snackbar.Add("Saved view filters could not be applied.", Severity.Warning);
        }
    }

    private Task OutboxEmptyPrimaryActionAsync()
    {
        return HasOutboxFilters ? ClearFiltersAsync() : LoadMessagesAsync();
    }

    private async Task SelectAsync(int id)
    {
        try
        {
            _selected = await OutboxService.GetAdminMessageAsync(id, await GetCurrentUserIdAsync());
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task RetryAsync(int id)
    {
        try
        {
            await OutboxService.RetryAsync(id, await GetCurrentUserIdAsync());
            Snackbar.Add("Outbox message queued for retry.", Severity.Success);
            await LoadMessagesAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task CancelAsync(int id)
    {
        try
        {
            await OutboxService.CancelAsync(id, await GetCurrentUserIdAsync());
            Snackbar.Add("Outbox message cancelled.", Severity.Success);
            await LoadMessagesAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private Task SelectAllVisibleOutboxMessagesAsync()
    {
        return SetAllVisibleOutboxMessagesSelectedAsync(true);
    }

    private Task SetAllVisibleOutboxMessagesSelectedAsync(bool selected)
    {
        foreach (var message in _messages)
        {
            if (selected)
            {
                _selectedOutboxMessageIds.Add(message.Id);
            }
            else
            {
                _selectedOutboxMessageIds.Remove(message.Id);
            }
        }

        return Task.CompletedTask;
    }

    private void ToggleOutboxSelection(int id, bool selected)
    {
        if (selected)
        {
            _selectedOutboxMessageIds.Add(id);
        }
        else
        {
            _selectedOutboxMessageIds.Remove(id);
        }
    }

    private void ClearOutboxSelection()
    {
        _selectedOutboxMessageIds.Clear();
    }

    private void RemoveHiddenOutboxSelections()
    {
        _selectedOutboxMessageIds.RemoveWhere(id => _messages.All(message => message.Id != id));
    }

    private async Task ConfirmBulkRetryAsync()
    {
        var selectedIds = VisibleSelectedOutboxIds;
        if (selectedIds.Count == 0)
        {
            Snackbar.Add("Select at least one visible outbox message first.", Severity.Info);
            return;
        }

        if (!await ConfirmAsync(
            "Retry selected notifications",
            $"This will queue {selectedIds.Count} selected notification outbox messages for another delivery attempt. Sent or cancelled items will be reported as failed.",
            "Retry selected",
            Color.Warning,
            Icons.Material.Filled.Replay))
        {
            return;
        }

        await RunBulkOutboxActionAsync(selectedIds, retry: true);
    }

    private async Task ConfirmBulkCancelAsync()
    {
        var selectedIds = VisibleSelectedOutboxIds;
        if (selectedIds.Count == 0)
        {
            Snackbar.Add("Select at least one visible outbox message first.", Severity.Info);
            return;
        }

        if (!await ConfirmAsync(
            "Cancel selected notifications",
            $"This will cancel {selectedIds.Count} selected notification outbox messages that have not already been sent.",
            "Cancel selected",
            Color.Error,
            Icons.Material.Filled.Cancel))
        {
            return;
        }

        await RunBulkOutboxActionAsync(selectedIds, retry: false);
    }

    private async Task RunBulkOutboxActionAsync(IReadOnlyCollection<int> selectedIds, bool retry)
    {
        _isBulkActionRunning = true;

        try
        {
            var adminUserId = await GetCurrentUserIdAsync();
            _bulkActionResult = retry
                ? await BulkOutboxActionService.RetryAsync(adminUserId, selectedIds)
                : await BulkOutboxActionService.CancelAsync(adminUserId, selectedIds);
            ClearOutboxSelection();
            Snackbar.Add(_bulkActionResult.Message, _bulkActionResult.Failed > 0 ? Severity.Warning : Severity.Success);
            await LoadMessagesAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        finally
        {
            _isBulkActionRunning = false;
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message, string confirmText, Color confirmColor, string icon)
    {
        var parameters = new DialogParameters
        {
            ["Title"] = title,
            ["Message"] = message,
            ["ConfirmText"] = confirmText,
            ["ConfirmColor"] = confirmColor,
            ["IconColor"] = confirmColor,
            ["Icon"] = icon
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>(title, parameters);
        var result = await dialog.Result;
        return result is not null && !result.Canceled;
    }

    private async Task ProcessNowAsync()
    {
        _isProcessingNow = true;

        try
        {
            var result = await OutboxProcessor.ProcessDueAsync(25);
            Snackbar.Add($"Processed {result.Processed} outbox messages. Sent {result.Sent}, failed {result.Failed}.", Severity.Info);
            await LoadMessagesAsync();
        }
        catch
        {
            Snackbar.Add("Outbox processor could not run right now.", Severity.Error);
        }
        finally
        {
            _isProcessingNow = false;
        }
    }

    private NotificationOutboxFilter BuildFilter()
    {
        return new NotificationOutboxFilter(
            TryParse<NotificationOutboxStatus>(_statusFilter),
            TryParse<NotificationChannel>(_channelFilter),
            TryParse<NotificationEventType>(_typeFilter),
            Search: _search);
    }

    private IReadOnlyList<string> BuildOutboxSavedViewSummaryLabels()
    {
        var labels = new List<string>();
        if (!string.IsNullOrWhiteSpace(_statusFilter) && TryParse<NotificationOutboxStatus>(_statusFilter) is { } status)
        {
            labels.Add($"Status: {FormatStatus(status)}");
        }

        if (!string.IsNullOrWhiteSpace(_channelFilter) && TryParse<NotificationChannel>(_channelFilter) is { } channel)
        {
            labels.Add($"Channel: {FormatChannel(channel)}");
        }

        if (!string.IsNullOrWhiteSpace(_typeFilter) && TryParse<NotificationEventType>(_typeFilter) is { } type)
        {
            labels.Add($"Type: {FormatType(type)}");
        }

        if (!string.IsNullOrWhiteSpace(_search))
        {
            labels.Add($"Search: {_search.Trim()}");
        }

        return labels;
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

    private static bool CanRetry(NotificationOutboxMessageDto message)
    {
        return message.Status is NotificationOutboxStatus.Pending or NotificationOutboxStatus.Processing or NotificationOutboxStatus.Failed or NotificationOutboxStatus.DeadLetter;
    }

    private static bool CanCancel(NotificationOutboxMessageDto message)
    {
        return message.Status is not NotificationOutboxStatus.Sent and not NotificationOutboxStatus.Cancelled;
    }

    private static TEnum? TryParse<TEnum>(string? value) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
    }

    private string FormatType(NotificationEventType type)
    {
        return _types.FirstOrDefault(description => description.NotificationType == type)?.DisplayName ?? type.ToString();
    }

    private static string FormatStatus(NotificationOutboxStatus status)
    {
        return status switch
        {
            NotificationOutboxStatus.DeadLetter => "Dead letter",
            _ => status.ToString()
        };
    }

    private static string FormatChannel(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.InApp => "In-app",
            _ => "Email"
        };
    }

    private static Color GetStatusColor(NotificationOutboxStatus status)
    {
        return status switch
        {
            NotificationOutboxStatus.Sent => Color.Success,
            NotificationOutboxStatus.Failed => Color.Error,
            NotificationOutboxStatus.DeadLetter => Color.Error,
            NotificationOutboxStatus.Pending => Color.Info,
            NotificationOutboxStatus.Processing => Color.Warning,
            NotificationOutboxStatus.Cancelled => Color.Default,
            _ => Color.Default
        };
    }

    private static Color GetChannelColor(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.Email => Color.Secondary,
            _ => Color.Primary
        };
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToLocalTime().ToString("dd MMM yyyy HH:mm");
    }

    private static string FormatNullableDateTime(DateTime? value)
    {
        return value is null ? "-" : FormatDateTime(value.Value);
    }

    private static string FormatRelated(NotificationOutboxMessageDto message)
    {
        return string.IsNullOrWhiteSpace(message.RelatedEntityType)
            ? "-"
            : $"{message.RelatedEntityType} #{message.RelatedEntityId}";
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

    private sealed record OutboxSavedViewState(
        string? Status,
        string? Channel,
        string? Type,
        string? Search);
}
