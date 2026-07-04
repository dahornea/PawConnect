using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminLostFoundPosts
{
    [Inject] private ILostFoundPostService LostFoundPostService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private static readonly IReadOnlyList<LostFoundPostStatus> StatusFilters = Enum.GetValues<LostFoundPostStatus>();

    private readonly Dictionary<int, string?> _rejectReasons = [];
    private readonly HashSet<int> _processingPostIds = [];
    private List<LostFoundPostListItemDto> _posts = [];
    private bool _isLoading = true;
    private string? _error;
    private string? _statusFilter = LostFoundPostStatus.PendingReview.ToString();
    private string? _postTypeFilter;
    private string? _keyword;

    protected override async Task OnInitializedAsync()
    {
        await LoadPostsAsync();
    }

    private async Task LoadPostsAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            var adminUserId = await GetCurrentUserIdAsync();
            _posts = (await LostFoundPostService.GetAdminPostsAsync(BuildFilter(), adminUserId)).ToList();
        }
        catch (InvalidOperationException ex)
        {
            _error = ex.Message;
        }
        catch
        {
            _error = "Lost and found posts could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ClearFiltersAsync()
    {
        _statusFilter = null;
        _postTypeFilter = null;
        _keyword = null;
        await LoadPostsAsync();
    }

    private async Task ApproveAsync(int postId)
    {
        if (!_processingPostIds.Add(postId))
        {
            return;
        }

        try
        {
            var adminUserId = await GetCurrentUserIdAsync();
            await LostFoundPostService.ApprovePostAsync(postId, adminUserId);
            Snackbar.Add("Lost and found post approved.", Severity.Success);
            await LoadPostsAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        finally
        {
            _processingPostIds.Remove(postId);
        }
    }

    private async Task RejectAsync(int postId)
    {
        if (!_processingPostIds.Add(postId))
        {
            return;
        }

        try
        {
            var adminUserId = await GetCurrentUserIdAsync();
            await LostFoundPostService.RejectPostAsync(postId, GetRejectReason(postId) ?? string.Empty, adminUserId);
            Snackbar.Add("Lost and found post rejected.", Severity.Info);
            await LoadPostsAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        finally
        {
            _processingPostIds.Remove(postId);
        }
    }

    private async Task CloseAsync(int postId)
    {
        if (!_processingPostIds.Add(postId))
        {
            return;
        }

        try
        {
            var adminUserId = await GetCurrentUserIdAsync();
            await LostFoundPostService.ClosePostAsync(postId, "Marked resolved by an administrator.", adminUserId, isAdmin: true);
            Snackbar.Add("Lost and found post marked as resolved.", Severity.Success);
            await LoadPostsAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        finally
        {
            _processingPostIds.Remove(postId);
        }
    }

    private async Task ReopenAsync(int postId)
    {
        if (!_processingPostIds.Add(postId))
        {
            return;
        }

        try
        {
            var adminUserId = await GetCurrentUserIdAsync();
            await LostFoundPostService.ReopenPostAsync(postId, adminUserId);
            Snackbar.Add("Lost and found post reopened.", Severity.Success);
            await LoadPostsAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        finally
        {
            _processingPostIds.Remove(postId);
        }
    }

    private LostFoundPostFilter BuildFilter()
    {
        return new LostFoundPostFilter(
            TryParse<LostFoundPostType>(_postTypeFilter),
            TryParse<LostFoundPostStatus>(_statusFilter),
            Keyword: EmptyToNull(_keyword));
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        return authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Current user could not be found.");
    }

    private string? GetRejectReason(int postId)
    {
        return _rejectReasons.TryGetValue(postId, out var reason) ? reason : null;
    }

    private void SetRejectReason(int postId, string value)
    {
        _rejectReasons[postId] = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private bool IsProcessing(int postId)
    {
        return _processingPostIds.Contains(postId);
    }

    private static TEnum? TryParse<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string FormatPostType(LostFoundPostType type)
    {
        return type == LostFoundPostType.Lost ? "Lost dog" : "Found dog";
    }

    private static string FormatStatus(LostFoundPostStatus status)
    {
        return status switch
        {
            LostFoundPostStatus.PendingReview => "Pending review",
            LostFoundPostStatus.Approved => "Open",
            LostFoundPostStatus.Rejected => "Rejected",
            LostFoundPostStatus.Closed => "Resolved",
            LostFoundPostStatus.Expired => "Expired",
            _ => status.ToString()
        };
    }

    private static Color GetStatusColor(LostFoundPostStatus status)
    {
        return status switch
        {
            LostFoundPostStatus.PendingReview => Color.Warning,
            LostFoundPostStatus.Approved => Color.Success,
            LostFoundPostStatus.Rejected => Color.Error,
            LostFoundPostStatus.Closed => Color.Default,
            LostFoundPostStatus.Expired => Color.Default,
            _ => Color.Default
        };
    }

    private static string BuildSubtitle(LostFoundPostListItemDto post)
    {
        var parts = new[]
        {
            post.DogName,
            post.BreedText,
            post.Size?.ToString(),
            post.CoatColor
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(" · ", parts);
    }

    private static string BuildLocation(LostFoundPostListItemDto post)
    {
        return string.Join(", ", new[]
        {
            post.Neighborhood,
            post.City,
            post.AddressOrAreaDescription
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
