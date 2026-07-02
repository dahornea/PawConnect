using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
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

public partial class Recommendations
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IAdopterProfileService AdopterProfileService { get; set; } = default!;
    [Inject] private IDogRecommendationService DogRecommendationService { get; set; } = default!;

    private AdopterProfile? _profile;
    private IReadOnlyList<DogRecommendationResult> _recommendations = [];
    private bool _isLoading = true;
    private string? _error;

    private bool ProfileNeedsAttention => _profile is not null &&
        (string.IsNullOrWhiteSpace(_profile.City) ||
         string.IsNullOrWhiteSpace(_profile.ExperienceWithDogs));

    private string MissingProfileFieldsText => string.Join(", ", GetMissingProfileFields());

    private string RecommendationSourceText => _recommendations.Any(recommendation => recommendation.UsedAiEnhancement)
        ? "AI-assisted explanations are enabled."
        : "Rule-based matches from your profile, saved dogs, and recent views.";

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

            _profile = await AdopterProfileService.GetProfileForUserAsync(user.Id);
            _recommendations = await DogRecommendationService.GetRecommendationsForAdopterAsync(user.Id, 12);
        }
        catch
        {
            _error = "Recommendations could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static string? GetDogImageUrl(Dog dog)
    {
        return DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images);
    }

    private static string GetShelterLine(Dog dog)
    {
        var shelterName = dog.Shelter?.Name?.Trim();
        var shelterCity = dog.Shelter?.City?.Trim();

        return (string.IsNullOrWhiteSpace(shelterName), string.IsNullOrWhiteSpace(shelterCity)) switch
        {
            (false, false) => $"{shelterName} · {shelterCity}",
            (false, true) => shelterName!,
            (true, false) => shelterCity!,
            _ => "Shelter details available on profile"
        };
    }

    private static Color GetStatusColor(DogStatus status)
    {
        return status switch
        {
            DogStatus.Available => Color.Success,
            DogStatus.Reserved => Color.Warning,
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

    private IReadOnlyList<string> GetMissingProfileFields()
    {
        if (_profile is null)
        {
            return ["profile details"];
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_profile.City))
        {
            missing.Add("city");
        }

        if (string.IsNullOrWhiteSpace(_profile.ExperienceWithDogs))
        {
            missing.Add("experience with dogs");
        }

        return missing.Count == 0 ? ["more preference details"] : missing;
    }

    private static string GetSummary(DogRecommendationResult recommendation)
    {
        return string.IsNullOrWhiteSpace(recommendation.ShortSummary)
            ? $"Why this dog may fit you: {recommendation.Reasons.FirstOrDefault() ?? "Their public profile matches part of your adopter profile."}"
            : recommendation.ShortSummary;
    }

    private static IReadOnlyList<DogRecommendationReason> GetVisibleReasons(DogRecommendationResult recommendation)
    {
        if (recommendation.ReasonCategories is { Count: > 0 })
        {
            return recommendation.ReasonCategories
                .OrderByDescending(reason => reason.Weight)
                .Take(4)
                .ToList();
        }

        return recommendation.Reasons
            .Take(3)
            .Select((reason, index) => new DogRecommendationReason("Match fit", reason, 100 - index))
            .ToList();
    }
}
