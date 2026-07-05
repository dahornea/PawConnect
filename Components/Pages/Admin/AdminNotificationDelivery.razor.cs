using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminNotificationDelivery
{
    [Inject] private INotificationDeliveryLogService DeliveryLogService { get; set; } = default!;
    [Inject] private INotificationPreferenceService PreferenceService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    private readonly IReadOnlyList<NotificationDeliveryStatus> _statuses = Enum.GetValues<NotificationDeliveryStatus>();
    private readonly IReadOnlyList<NotificationChannel> _channels = Enum.GetValues<NotificationChannel>();
    private IReadOnlyList<NotificationTypeDescriptionDto> _types = [];
    private List<NotificationDeliveryLogDto> _logs = [];
    private NotificationDeliverySummaryDto _summary = new(0, 0, 0, 0, 0, 0);
    private string? _statusFilter;
    private string? _channelFilter;
    private string? _typeFilter;
    private string? _search;
    private string? _error;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        _types = PreferenceService.GetNotificationTypes();
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            var adminUserId = await GetCurrentUserIdAsync();
            var filter = BuildFilter();
            _logs = (await DeliveryLogService.GetAdminDeliveryLogsAsync(filter, adminUserId)).ToList();
            _summary = await DeliveryLogService.GetAdminDeliverySummaryAsync(filter, adminUserId);
        }
        catch (InvalidOperationException ex)
        {
            _error = ex.Message;
        }
        catch
        {
            _error = "Notification delivery logs could not be loaded right now.";
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
        _fromDate = null;
        _toDate = null;
        await LoadLogsAsync();
    }

    private NotificationDeliveryLogFilter BuildFilter()
    {
        return new NotificationDeliveryLogFilter(
            TryParse<NotificationDeliveryStatus>(_statusFilter),
            TryParse<NotificationChannel>(_channelFilter),
            TryParse<NotificationEventType>(_typeFilter),
            _fromDate?.Date,
            _toDate?.Date,
            _search);
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

    private static TEnum? TryParse<TEnum>(string? value) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
    }

    private string FormatType(NotificationEventType type)
    {
        return _types.FirstOrDefault(description => description.NotificationType == type)?.DisplayName ?? type.ToString();
    }

    private static string FormatStatus(NotificationDeliveryStatus status)
    {
        return status switch
        {
            NotificationDeliveryStatus.DisabledByPreference => "Disabled by preference",
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

    private static Color GetStatusColor(NotificationDeliveryStatus status)
    {
        return status switch
        {
            NotificationDeliveryStatus.Sent => Color.Success,
            NotificationDeliveryStatus.Failed => Color.Error,
            NotificationDeliveryStatus.Pending => Color.Info,
            NotificationDeliveryStatus.DisabledByPreference => Color.Warning,
            NotificationDeliveryStatus.Skipped => Color.Default,
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
