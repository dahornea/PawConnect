using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages;

public partial class Notifications
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private INotificationCenterService NotificationCenterService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private IReadOnlyList<NotificationCenterGroupDto> _groups = [];
    private IReadOnlyList<NotificationCategory> _availableCategories = [];
    private string? _userId;
    private string? _searchTerm;
    private string? _error;
    private bool _isLoading = true;
    private int _unreadCount;
    private int _totalCount;
    private NotificationCategory? _categoryFilter;
    private NotificationReadState _readState = NotificationReadState.All;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await LoadNotificationsAsync();
    }

    private async Task LoadNotificationsAsync()
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            _groups = [];
            _availableCategories = [];
            _error = "Current user could not be found.";
            _isLoading = false;
            return;
        }

        _isLoading = true;
        _error = null;

        try
        {
            var result = await NotificationCenterService.GetNotificationsAsync(
                _userId,
                new NotificationCenterQuery(_categoryFilter, _readState, _searchTerm));

            _groups = result.Groups;
            _availableCategories = result.AvailableCategories;
            _unreadCount = result.UnreadCount;
            _totalCount = result.TotalCount;
        }
        catch
        {
            _error = "Notifications could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SetCategoryAsync(NotificationCategory? category)
    {
        _categoryFilter = category;
        await LoadNotificationsAsync();
    }

    private async Task SetReadStateAsync(NotificationReadState state)
    {
        _readState = state;
        await LoadNotificationsAsync();
    }

    private async Task ApplySearchAsync()
    {
        await LoadNotificationsAsync();
    }

    private async Task ClearFiltersAsync()
    {
        _categoryFilter = null;
        _readState = NotificationReadState.All;
        _searchTerm = null;
        await LoadNotificationsAsync();
    }

    private async Task MarkAsReadAsync(NotificationCenterItemDto notification)
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        await NotificationCenterService.MarkAsReadAsync(notification.Id, _userId);
        await LoadNotificationsAsync();
    }

    private async Task MarkAsUnreadAsync(NotificationCenterItemDto notification)
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        await NotificationCenterService.MarkAsUnreadAsync(notification.Id, _userId);
        await LoadNotificationsAsync();
    }

    private async Task MarkAllAsReadAsync()
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        await NotificationCenterService.MarkAllAsReadAsync(_userId);
        await LoadNotificationsAsync();
        Snackbar.Add("Notifications marked as read.", Severity.Success);
    }

    private async Task DismissAsync(NotificationCenterItemDto notification)
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        await NotificationCenterService.DismissAsync(notification.Id, _userId);
        await LoadNotificationsAsync();
        Snackbar.Add("Notification dismissed.", Severity.Success);
    }

    private async Task OpenNotificationAsync(NotificationCenterItemDto notification)
    {
        if (!string.IsNullOrWhiteSpace(_userId) && !notification.IsRead)
        {
            await NotificationCenterService.MarkAsReadAsync(notification.Id, _userId);
        }

        if (!string.IsNullOrWhiteSpace(notification.RelatedUrl))
        {
            NavigationManager.NavigateTo(notification.RelatedUrl);
            return;
        }

        await LoadNotificationsAsync();
    }

    private bool HasActiveFilters =>
        _categoryFilter.HasValue ||
        _readState != NotificationReadState.All ||
        !string.IsNullOrWhiteSpace(_searchTerm);

    private Variant GetCategoryVariant(NotificationCategory? category)
    {
        return _categoryFilter == category ? Variant.Filled : Variant.Outlined;
    }

    private Variant GetReadStateVariant(NotificationReadState state)
    {
        return _readState == state ? Variant.Filled : Variant.Outlined;
    }

    private static string GetCategoryLabel(NotificationCategory category)
    {
        return PawConnect.Services.NotificationCenterService.GetCategoryLabel(category);
    }

    private static string GetCategoryIcon(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.Adoption => Icons.Material.Filled.AssignmentTurnedIn,
            NotificationCategory.ShelterApplication => Icons.Material.Filled.Business,
            NotificationCategory.Resource => Icons.Material.Filled.Inventory2,
            NotificationCategory.Report => Icons.Material.Filled.PictureAsPdf,
            NotificationCategory.System => Icons.Material.Filled.Info,
            NotificationCategory.Transfer => Icons.Material.Filled.SwapHoriz,
            NotificationCategory.Volunteer => Icons.Material.Filled.VolunteerActivism,
            NotificationCategory.SavedSearch => Icons.Material.Filled.SavedSearch,
            _ => Icons.Material.Filled.Notifications
        };
    }

    private static Color GetCategoryColor(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.Adoption => Color.Info,
            NotificationCategory.ShelterApplication => Color.Secondary,
            NotificationCategory.Resource => Color.Warning,
            NotificationCategory.Report => Color.Success,
            NotificationCategory.System => Color.Default,
            NotificationCategory.Transfer => Color.Primary,
            NotificationCategory.Volunteer => Color.Tertiary,
            NotificationCategory.SavedSearch => Color.Primary,
            _ => Color.Info
        };
    }

    private static string GetTypeIcon(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => Icons.Material.Filled.CheckCircle,
            NotificationType.Warning => Icons.Material.Filled.Warning,
            NotificationType.Error => Icons.Material.Filled.Error,
            _ => Icons.Material.Filled.Info
        };
    }

    private static Color GetTypeColor(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => Color.Success,
            NotificationType.Warning => Color.Warning,
            NotificationType.Error => Color.Error,
            _ => Color.Info
        };
    }
}
