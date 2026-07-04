using Microsoft.AspNetCore.Components;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.LostFound;

public partial class LostFoundList
{
    [Inject] private ILostFoundPostService LostFoundPostService { get; set; } = default!;

    private List<LostFoundPostListItemDto> _posts = [];
    private bool _isLoading = true;
    private string? _error;
    private string? _keyword;
    private string? _cityFilter;
    private string _postTypeFilter = string.Empty;
    private string _statusFilter = string.Empty;

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
            _posts = (await LostFoundPostService.GetPublicPostsAsync(BuildFilter())).ToList();
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
        _keyword = null;
        _cityFilter = null;
        _postTypeFilter = string.Empty;
        _statusFilter = string.Empty;
        await LoadPostsAsync();
    }

    private LostFoundPostFilter BuildFilter()
    {
        return new LostFoundPostFilter(
            TryParse<LostFoundPostType>(_postTypeFilter),
            TryParse<LostFoundPostStatus>(_statusFilter),
            EmptyToNull(_cityFilter),
            Keyword: EmptyToNull(_keyword));
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
        return type == LostFoundPostType.Lost ? "Lost" : "Found";
    }

    private static string FormatStatus(LostFoundPostStatus status)
    {
        return status switch
        {
            LostFoundPostStatus.Approved => "Open",
            LostFoundPostStatus.Closed => "Resolved",
            LostFoundPostStatus.PendingReview => "Pending",
            LostFoundPostStatus.Rejected => "Rejected",
            LostFoundPostStatus.Expired => "Expired",
            _ => status.ToString()
        };
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

    private static string FormatDateLabel(LostFoundPostListItemDto post)
    {
        var label = post.PostType == LostFoundPostType.Lost ? "Last seen" : "Found";
        return $"{label}: {post.LastSeenOrFoundDate:dd MMM yyyy}";
    }

    private static string GetImageAlt(LostFoundPostListItemDto post)
    {
        var name = string.IsNullOrWhiteSpace(post.DogName) ? "dog" : post.DogName;
        return $"{FormatPostType(post.PostType)} dog photo for {name}";
    }
}
