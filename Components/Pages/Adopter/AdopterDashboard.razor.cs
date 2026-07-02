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

namespace PawConnect.Components.Pages.Adopter;

public partial class AdopterDashboard
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IAdopterProfileService AdopterProfileService { get; set; } = default!;
    [Inject] private IFavoriteDogService FavoriteDogService { get; set; } = default!;
    [Inject] private IAdoptionRequestService AdoptionRequestService { get; set; } = default!;
    [Inject] private IRecentlyViewedDogService RecentlyViewedDogService { get; set; } = default!;
    [Inject] private IDogRecommendationService DogRecommendationService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private AdopterProfile? _profile;
    private AdoptionRequestSummary _requestSummary = new(0, 0, 0);
    private List<AdoptionRequest> _recentRequests = [];
    private List<FavoriteDog> _recentFavorites = [];
    private List<RecentlyViewedDog> _recentlyViewedDogs = [];
    private IReadOnlyList<DogRecommendationResult> _recommendedDogs = [];
    private bool _isLoading = true;
    private bool _isClearingRecentlyViewed;
    private string? _error;
    private string? _userEmail;
    private string? _currentUserId;
    private int _favoriteCount;

    private string DisplayName => !string.IsNullOrWhiteSpace(_profile?.FullName)
        ? _profile.FullName
        : _userEmail ?? "Adopter";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(authState.User);
            if (user is null)
            {
                _error = "Current adopter account could not be found.";
                return;
            }

            _currentUserId = user.Id;
            _userEmail = user.Email;
            _profile = await AdopterProfileService.GetProfileForUserAsync(user.Id);
            _favoriteCount = await FavoriteDogService.GetFavoriteCountForUserAsync(user.Id);
            _recentFavorites = await FavoriteDogService.GetRecentFavoritesForUserAsync(user.Id, 3);
            _requestSummary = await AdoptionRequestService.GetAdoptionRequestSummaryForUserAsync(user.Id);
            _recentRequests = await AdoptionRequestService.GetRecentRequestsForAdopterAsync(user.Id, 5);
            _recentlyViewedDogs = await RecentlyViewedDogService.GetRecentlyViewedDogsAsync(user.Id, 6);
            _recommendedDogs = await DogRecommendationService.GetRecommendationsForAdopterAsync(user.Id, 3);
        }
        catch
        {
            _error = "Adopter dashboard could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static Color GetRequestStatusColor(AdoptionRequestStatus status)
    {
        return status switch
        {
            AdoptionRequestStatus.Pending => Color.Warning,
            AdoptionRequestStatus.VisitConfirmed => Color.Info,
            AdoptionRequestStatus.Accepted => Color.Success,
            AdoptionRequestStatus.Rejected => Color.Error,
            AdoptionRequestStatus.Cancelled => Color.Default,
            _ => Color.Default
        };
    }

    private static Color GetDogStatusColor(DogStatus status)
    {
        return status switch
        {
            DogStatus.Available => Color.Success,
            DogStatus.Reserved => Color.Warning,
            DogStatus.Adopted => Color.Default,
            DogStatus.InTreatment => Color.Info,
            _ => Color.Default
        };
    }

    private static Color GetMatchColor(string matchLevel)
    {
        return matchLevel switch
        {
            "Excellent match" => Color.Success,
            "Good match" => Color.Primary,
            _ => Color.Default
        };
    }

    private static string GetBestRecommendationReason(DogRecommendationResult recommendation)
    {
        return recommendation.ReasonCategories?
            .OrderByDescending(reason => reason.Weight)
            .Select(reason => reason.Text)
            .FirstOrDefault()
            ?? recommendation.Reasons.FirstOrDefault()
            ?? "Matches part of your adopter profile.";
    }

    private async Task ClearRecentlyViewedAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Current adopter account could not be found.", Severity.Error);
            return;
        }

        _isClearingRecentlyViewed = true;

        try
        {
            await RecentlyViewedDogService.ClearRecentlyViewedAsync(_currentUserId);
            _recentlyViewedDogs = [];
            Snackbar.Add("Recently viewed dogs cleared.", Severity.Success);
        }
        catch
        {
            Snackbar.Add("Could not clear recently viewed dogs. Please try again.", Severity.Error);
        }
        finally
        {
            _isClearingRecentlyViewed = false;
        }
    }

    private static string? GetDogImageUrl(Dog dog)
    {
        return DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images);
    }
}

