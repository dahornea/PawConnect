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

namespace PawConnect.Components.Shared;

public partial class NotificationBell
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private INotificationService NotificationService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private List<Notification> _notifications = [];
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
            _notifications = await NotificationService.GetNotificationsForUserAsync(_userId, 40);
            _unreadCount = await NotificationService.GetUnreadCountAsync(_userId);
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

        await NotificationService.MarkAllAsReadAsync(_userId);
        await LoadNotificationsAsync();
        Snackbar.Add("Notifications marked as read.", Severity.Success);
    }

    private async Task OpenNotificationAsync(NotificationDisplayItem item)
    {
        if (!string.IsNullOrWhiteSpace(_userId) && !item.IsRead)
        {
            foreach (var notification in item.Notifications.Where(notification => !notification.IsRead))
            {
                await NotificationService.MarkAsReadAsync(notification.Id, _userId);
            }
        }

        if (!string.IsNullOrWhiteSpace(item.Link))
        {
            _isOpen = false;
            NavigationManager.NavigateTo(item.Link);
        }
        else
        {
            await LoadNotificationsAsync();
        }
    }

    private List<NotificationDisplayItem> BuildDropdownItems()
    {
        return _notifications
            .GroupBy(notification => new
            {
                notification.Category,
                notification.Title,
                notification.Message,
                Day = notification.CreatedAt.ToLocalTime().Date
            })
            .Select(group =>
            {
                var notifications = group
                    .OrderByDescending(notification => notification.CreatedAt)
                    .ThenByDescending(notification => notification.Id)
                    .ToList();

                return new NotificationDisplayItem(notifications);
            })
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(DropdownNotificationLimit)
            .ToList();
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

    private static string GetCategoryClass(NotificationCategory category)
    {
        return category switch
        {
            NotificationCategory.ShelterApplication => "category-shelter",
            NotificationCategory.Resource => "category-resource",
            NotificationCategory.Report => "category-report",
            NotificationCategory.System => "category-system",
            NotificationCategory.SavedSearch => "category-saved-search",
            _ => "category-adoption"
        };
    }

    private sealed class NotificationDisplayItem
    {
        public NotificationDisplayItem(List<Notification> notifications)
        {
            Notifications = notifications;
            var latest = notifications[0];
            Id = latest.Id;
            Title = latest.Title;
            Message = latest.Message;
            Category = latest.Category;
            Link = latest.Link;
            CreatedAt = latest.CreatedAt;
            IsRead = notifications.All(notification => notification.IsRead);
            SimilarCount = Math.Max(0, notifications.Count - 1);
        }

        public IReadOnlyList<Notification> Notifications { get; }

        public int Id { get; }

        public string Title { get; }

        public string Message { get; }

        public NotificationCategory Category { get; }

        public string? Link { get; }

        public DateTime CreatedAt { get; }

        public bool IsRead { get; }

        public int SimilarCount { get; }

        public string TimeText => SimilarCount > 0
            ? $"Latest: {CreatedAt.ToLocalTime():dd MMM yyyy HH:mm}"
            : CreatedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm");
    }
}


