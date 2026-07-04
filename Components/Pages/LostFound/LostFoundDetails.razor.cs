using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.LostFound;

public partial class LostFoundDetails
{
    [Inject] private ILostFoundPostService LostFoundPostService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Parameter]
    public int Id { get; set; }

    private LostFoundPostDetailsDto? _post;
    private bool _isLoading = true;
    private string? _error;
    private string? _currentUserId;
    private bool _currentUserIsAdmin;
    private bool _isClosing;

    private bool CanShowContact => _post is not null &&
        (!string.IsNullOrWhiteSpace(_post.PublicContactEmail) || !string.IsNullOrWhiteSpace(_post.PublicContactPhone));
    private bool CanClosePost => _post?.Status == LostFoundPostStatus.Approved &&
        !string.IsNullOrWhiteSpace(_currentUserId) &&
        (string.Equals(_post.CreatedByUserId, _currentUserId, StringComparison.Ordinal) || _currentUserIsAdmin);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadCurrentUserAsync();
            _post = await LostFoundPostService.GetVisibleDetailsAsync(Id, _currentUserId, _currentUserIsAdmin);
        }
        catch
        {
            _error = "Lost and found post details could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadCurrentUserAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _currentUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _currentUserIsAdmin = user.IsInRole(IdentitySeedData.AdminRole);
    }

    private async Task ClosePostAsync()
    {
        if (_post is null || string.IsNullOrWhiteSpace(_currentUserId) || _isClosing)
        {
            return;
        }

        _isClosing = true;
        try
        {
            _post = await LostFoundPostService.ClosePostAsync(
                _post.Id,
                "Marked resolved by the post owner.",
                _currentUserId,
                _currentUserIsAdmin);
            Snackbar.Add("Lost and found post marked as resolved.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("The post could not be closed right now.", Severity.Error);
        }
        finally
        {
            _isClosing = false;
        }
    }

    private string BuildSubtitle()
    {
        if (_post is null)
        {
            return string.Empty;
        }

        var parts = new[]
        {
            _post.DogName,
            _post.BreedText,
            _post.Size?.ToString()
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(" · ", parts);
    }

    private string BuildLocation()
    {
        if (_post is null)
        {
            return string.Empty;
        }

        return string.Join(", ", new[]
        {
            _post.Neighborhood,
            _post.City,
            _post.AddressOrAreaDescription
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private string GetImageAlt()
    {
        if (_post is null)
        {
            return "Lost or found dog photo";
        }

        var name = string.IsNullOrWhiteSpace(_post.DogName) ? "dog" : _post.DogName;
        return $"{FormatPostType(_post.PostType)} dog photo for {name}";
    }

    private static string FormatPostType(LostFoundPostType type)
    {
        return type == LostFoundPostType.Lost ? "Lost dog" : "Found dog";
    }

    private static string FormatStatus(LostFoundPostStatus status)
    {
        return status switch
        {
            LostFoundPostStatus.Approved => "Open",
            LostFoundPostStatus.Closed => "Resolved",
            LostFoundPostStatus.PendingReview => "Pending review",
            LostFoundPostStatus.Rejected => "Rejected",
            LostFoundPostStatus.Expired => "Expired",
            _ => status.ToString()
        };
    }

    private static Color GetPostTypeColor(LostFoundPostType type)
    {
        return type == LostFoundPostType.Lost ? Color.Warning : Color.Success;
    }

    private static Color GetStatusColor(LostFoundPostStatus status)
    {
        return status switch
        {
            LostFoundPostStatus.Approved => Color.Success,
            LostFoundPostStatus.Closed => Color.Default,
            LostFoundPostStatus.PendingReview => Color.Warning,
            LostFoundPostStatus.Rejected => Color.Error,
            LostFoundPostStatus.Expired => Color.Default,
            _ => Color.Default
        };
    }
}
