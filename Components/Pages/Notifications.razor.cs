using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages;

public partial class Notifications
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private INotificationService NotificationService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private List<Notification> _notifications = [];
    private string? _userId;
    private bool _isLoading = true;
    private bool _unreadOnly;
    private int _unreadCount;
    private NotificationCategory? _categoryFilter;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await LoadNotificationsAsync();
    }

    private async Task SetFilterAsync(NotificationCategory? category, bool unreadOnly)
    {
        _categoryFilter = category;
        _unreadOnly = unreadOnly;
        await LoadNotificationsAsync();
    }

    private async Task LoadNotificationsAsync()
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            _notifications = [];
            _isLoading = false;
            return;
        }

        _isLoading = true;
        try
        {
            _notifications = await NotificationService.GetNotificationsForUserAsync(_userId, _categoryFilter, _unreadOnly, 200);
            _unreadCount = await NotificationService.GetUnreadCountAsync(_userId);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task MarkAsReadAsync(Notification notification)
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        await NotificationService.MarkAsReadAsync(notification.Id, _userId);
        await LoadNotificationsAsync();
    }

    private async Task MarkAllAsReadAsync()
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        await NotificationService.MarkAllAsReadAsync(_userId);
        await LoadNotificationsAsync();
        Snackbar.Add("Notifications marked as read.", Severity.Success);
    }

    private async Task DeleteNotificationAsync(Notification notification)
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        await NotificationService.DeleteNotificationAsync(notification.Id, _userId);
        await LoadNotificationsAsync();
        Snackbar.Add("Notification deleted.", Severity.Success);
    }

    private async Task OpenNotificationAsync(Notification notification)
    {
        if (!string.IsNullOrWhiteSpace(_userId) && !notification.IsRead)
        {
            await NotificationService.MarkAsReadAsync(notification.Id, _userId);
        }

        if (!string.IsNullOrWhiteSpace(notification.Link))
        {
            NavigationManager.NavigateTo(notification.Link);
        }
    }

    private Variant GetFilterVariant(NotificationCategory? category, bool unreadOnly)
    {
        return _categoryFilter == category && _unreadOnly == unreadOnly ? Variant.Filled : Variant.Outlined;
    }

    private static string GetCategoryLabel(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.ShelterApplication => "Shelter Applications",
            NotificationCategory.Resource => "Resources",
            NotificationCategory.Report => "Reports",
            NotificationCategory.System => "System",
            NotificationCategory.SavedSearch => "Saved Searches",
            _ => "Adoption"
        };
    }

    private static string GetCategoryIcon(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.ShelterApplication => Icons.Material.Filled.Business,
            NotificationCategory.Resource => Icons.Material.Filled.Inventory,
            NotificationCategory.Report => Icons.Material.Filled.PictureAsPdf,
            NotificationCategory.System => Icons.Material.Filled.Info,
            NotificationCategory.SavedSearch => Icons.Material.Filled.SavedSearch,
            _ => Icons.Material.Filled.Pets
        };
    }

    private static Color GetCategoryColor(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.Resource => Color.Warning,
            NotificationCategory.Report => Color.Success,
            NotificationCategory.System => Color.Default,
            NotificationCategory.ShelterApplication => Color.Secondary,
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


