using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Shared;

public partial class NotificationBell
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private INotificationCenterService NotificationCenterService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private IReadOnlyList<NotificationCenterItemDto> _notifications = [];
    private string? _userId;
    private int _unreadCount;
    private bool _isLoading;
    private bool _isOpen;
    private const int DropdownNotificationLimit = 8;

    private string UnreadCountText => _unreadCount > 9 ? "9+" : _unreadCount.ToString();

    protected override async Task OnInitializedAsync()
    {
        await ResolveUserAsync();
        await LoadNotificationsAsync();
    }

    private async Task ResolveUserAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private async Task LoadNotificationsAsync()
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            await ResolveUserAsync();
        }

        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        _isLoading = true;
        try
        {
            _notifications = await NotificationCenterService.GetPreviewAsync(_userId, DropdownNotificationLimit);
            _unreadCount = await NotificationCenterService.GetUnreadCountAsync(_userId);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ToggleNotificationsAsync()
    {
        _isOpen = !_isOpen;

        if (_isOpen)
        {
            await LoadNotificationsAsync();
        }
    }

    private void CloseNotifications()
    {
        _isOpen = false;
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

    private async Task OpenNotificationAsync(NotificationCenterItemDto item)
    {
        if (!string.IsNullOrWhiteSpace(_userId) && !item.IsRead)
        {
            await NotificationCenterService.MarkAsReadAsync(item.Id, _userId);
        }

        _isOpen = false;

        if (!string.IsNullOrWhiteSpace(item.RelatedUrl))
        {
            NavigationManager.NavigateTo(item.RelatedUrl);
        }
        else
        {
            NavigationManager.NavigateTo("/notifications");
        }
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

    private static string GetCategoryClass(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.ShelterApplication => "category-shelter",
            NotificationCategory.Resource => "category-resource",
            NotificationCategory.Report => "category-report",
            NotificationCategory.System => "category-system",
            NotificationCategory.SavedSearch => "category-saved-search",
            NotificationCategory.Transfer => "category-transfer",
            NotificationCategory.Volunteer => "category-volunteer",
            _ => "category-adoption"
        };
    }
}
