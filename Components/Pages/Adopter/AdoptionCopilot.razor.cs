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

public partial class AdoptionCopilot
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IAdoptionCopilotService AdoptionCopilotService { get; set; } = default!;
    [Inject] private ICopilotStateService CopilotStateService { get; set; } = default!;
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IFavoriteDogService FavoriteDogService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private static readonly string[] SuggestedPrompts =
    [
        "Find a calm dog for an apartment",
        "Show me friendly dogs near Cluj",
        "I want an active dog for a house with a yard",
        "Find dogs similar to my favorites"
    ];

    private static readonly Dictionary<string, object> CopilotInputAttributes = new()
    {
        ["spellcheck"] = "false",
        ["autocomplete"] = "off",
        ["autocapitalize"] = "off",
        ["autocorrect"] = "off"
    };

    private string? _query;
    private string? _currentUserId;
    private string? _error;
    private bool _isAsking;
    private HashSet<int> _favoriteDogIds = [];
    private AdoptionCopilotResponse? _response;

    private bool CanAsk => !_isAsking && !string.IsNullOrWhiteSpace(_query);

    protected override async Task OnInitializedAsync()
    {
        await LoadCurrentUserAsync();
        await LoadFavoriteStateAsync();
        await RestoreCopilotStateAsync();
    }

    private async Task AskCopilotAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            _error = "Current adopter account could not be found.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_query))
        {
            _error = "Describe the kind of dog you are looking for.";
            return;
        }

        _isAsking = true;
        _error = null;

        try
        {
            _response = await AdoptionCopilotService.AskAsync(_currentUserId, _query);
            CopilotStateService.SaveState(_currentUserId, _query, _response);
            await LoadFavoriteStateAsync();
        }
        catch
        {
            _error = "The Adoption Copilot could not load suggestions right now.";
        }
        finally
        {
            _isAsking = false;
        }
    }

    private async Task UsePromptAsync(string prompt)
    {
        _query = prompt;
        await AskCopilotAsync();
    }

    private void ClearConversation()
    {
        _query = null;
        _error = null;
        _response = null;
        CopilotStateService.ClearState(_currentUserId);
    }

    private async Task LoadCurrentUserAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(authState.User);
        _currentUserId = user?.Id;
    }

    private async Task LoadFavoriteStateAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            _favoriteDogIds = [];
            return;
        }

        _favoriteDogIds = await FavoriteDogService.GetFavoriteDogIdsForUserAsync(_currentUserId);
    }

    private async Task RestoreCopilotStateAsync()
    {
        var state = CopilotStateService.GetState(_currentUserId);
        if (state is null)
        {
            return;
        }

        _query = state.LastQuery;
        _response = await BuildResponseFromStateAsync(state);
    }

    private async Task<AdoptionCopilotResponse> BuildResponseFromStateAsync(CopilotSessionState state)
    {
        var results = new List<AdoptionCopilotDogResult>();
        foreach (var savedResult in state.LastResults)
        {
            var dog = await DogService.GetDogDetailsAsync(savedResult.DogId);
            if (dog?.Status is not (DogStatus.Available or DogStatus.Reserved))
            {
                continue;
            }

            results.Add(new AdoptionCopilotDogResult(
                dog.Id,
                dog,
                savedResult.ScorePercent,
                savedResult.MatchLabel,
                savedResult.Reasons,
                savedResult.SuggestedNextAction,
                savedResult.DistanceKm,
                savedResult.UsedAiEnhancement,
                savedResult.MatchedCriteria,
                savedResult.DisplayTags,
                savedResult.CautionTags));
        }

        return new AdoptionCopilotResponse(
            state.LastAssistantMessage,
            results,
            state.LastUsedOpenAi,
            state.LastUsedSemanticSearch,
            state.LastUsedToolCalling,
            state.LastFallbackReason,
            state.LastAppliedConstraints);
    }

    private async Task ToggleFavoriteAsync(Dog dog)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Please log in with an adopter account to save favorite dogs.", Severity.Info);
            NavigationManager.NavigateTo($"/Account/Login?returnUrl={Uri.EscapeDataString(NavigationManager.Uri)}");
            return;
        }

        try
        {
            if (_favoriteDogIds.Contains(dog.Id))
            {
                await FavoriteDogService.RemoveFavoriteAsync(_currentUserId, dog.Id);
                _favoriteDogIds.Remove(dog.Id);
                Snackbar.Add("Dog removed from favorites.", Severity.Success);
            }
            else
            {
                await FavoriteDogService.AddFavoriteAsync(_currentUserId, dog.Id);
                _favoriteDogIds.Add(dog.Id);
                Snackbar.Add("Dog added to favorites.", Severity.Success);
            }
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not update favorites. Please try again.", Severity.Error);
        }
    }

    private static string? GetDogImageUrl(Dog dog)
    {
        return DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images);
    }

    private static string GetShelterLine(Dog dog)
    {
        var shelterName = dog.Shelter?.Name?.Trim();
        var shelterNeighborhood = dog.Shelter?.Neighborhood?.Trim();
        var shelterCity = dog.Shelter?.City?.Trim();
        var shelterLocation = string.Join(", ", new[] { shelterNeighborhood, shelterCity }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return (string.IsNullOrWhiteSpace(shelterName), string.IsNullOrWhiteSpace(shelterLocation)) switch
        {
            (false, false) => $"{shelterName} · {shelterLocation}",
            (false, true) => shelterName!,
            (true, false) => shelterLocation,
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
            "Strong match" => Color.Success,
            "Good match" => Color.Primary,
            "Exact match" => Color.Success,
            "Matches request" => Color.Success,
            "Exact filter match" => Color.Success,
            "Matching result" => Color.Success,
            "Possible match" => Color.Default,
            "Potential match" => Color.Default,
            "Weak match" => Color.Default,
            "Low match" => Color.Default,
            _ => Color.Default
        };
    }

    private static bool IsFilterMatchLabel(string? matchLevel)
    {
        return matchLevel is "Exact match" or "Matches request" or "Exact filter match" or "Matching result";
    }

    private static IReadOnlyList<string> GetReasonDisplays(IReadOnlyList<string> reasons, IReadOnlySet<string> matchedLabels)
    {
        return reasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Select(ShortenReason)
            .Where(reason => !IsDuplicateMatchedReason(reason, matchedLabels))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static IReadOnlyList<string> GetEvidenceDisplays(
        IReadOnlyList<string>? displayTags,
        IReadOnlyList<string> fallbackReasons,
        IReadOnlySet<string> matchedLabels)
    {
        var tags = displayTags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        return tags.Count > 0
            ? tags.Take(4).ToList()
            : GetReasonDisplays(fallbackReasons, matchedLabels);
    }

    private static IReadOnlyList<string> GetCautionDisplays(IReadOnlyList<string>? cautionTags, DogStatus status)
    {
        var tags = cautionTags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (status == DogStatus.Reserved &&
            !tags.Contains("Reserved - availability may change", StringComparer.OrdinalIgnoreCase))
        {
            tags.Add("Reserved - availability may change");
        }

        return tags.Take(2).ToList();
    }

    private static string FormatConstraintChip(AdoptionCopilotConstraint constraint)
    {
        var label = (constraint.Label ?? string.Empty).Trim();
        var value = (constraint.Value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(label)
            ? value
            : string.IsNullOrWhiteSpace(value)
                ? label
                : $"{label}: {value}";
    }

    private static bool IsDuplicateMatchedReason(string reason, IReadOnlySet<string> matchedLabels)
    {
        if (matchedLabels.Count == 0)
        {
            return false;
        }

        var lower = reason.ToLowerInvariant();
        return matchedLabels.Contains("Age") && lower.Contains("age") ||
            matchedLabels.Contains("Size") && lower.Contains("size") ||
            (matchedLabels.Contains("Neighborhood") || matchedLabels.Contains("Location")) && (lower.Contains("neighborhood") || lower.StartsWith("in ", StringComparison.OrdinalIgnoreCase)) ||
            matchedLabels.Contains("Status") && (lower.Contains("available") || lower.Contains("reserved") || lower.Contains("status")) ||
            matchedLabels.Contains("Temperament") && lower.Contains("temperament");
    }

    private static string ShortenReason(string reason)
    {
        var normalized = reason.Trim();
        var lower = normalized.ToLowerInvariant();

        if (lower.Contains("short walks") || lower.Contains("slow walks"))
        {
            return "Short walks";
        }

        if (lower.Contains("indoor rest") || lower.Contains("smaller spaces"))
        {
            return "Indoor rest";
        }

        if (lower.Contains("settles"))
        {
            return "Settles quickly";
        }

        if (lower.Contains("quiet routine") || lower.Contains("predictable"))
        {
            return "Quiet routine";
        }

        if (lower.Contains("lower activity") || lower.Contains("low activity"))
        {
            return "Lower activity fit";
        }

        if (lower.Contains("apartment lifestyle") || lower.Contains("apartment-friendly fit"))
        {
            return "Apartment lifestyle fit";
        }

        if (lower.Contains("small size"))
        {
            return "Small size";
        }

        if (lower.Contains("medium size"))
        {
            return "Medium size";
        }

        if (lower.Contains("relaxed") || lower.Contains("calm"))
        {
            return "Relaxed temperament";
        }

        if (lower.Contains("experience"))
        {
            return "Experienced adopter fit";
        }

        if (lower.Contains("beginner") || lower.Contains("first time"))
        {
            return "Easier first-dog fit";
        }

        if (lower.Contains("social"))
        {
            return "Social temperament";
        }

        if (lower.Contains("friendly"))
        {
            return "Friendly temperament";
        }

        if (lower.Contains("saved") || lower.Contains("favorite") || lower.Contains("recently viewed"))
        {
            return "Matches saved dogs";
        }

        if (lower.Contains("same city") || lower.Contains("near") || lower.Contains("close") || lower.Contains("distance"))
        {
            return "Near your area";
        }

        if (lower.Contains("yard") || lower.Contains("house"))
        {
            return "Yard fit";
        }

        if (lower.Contains("older children") || lower.Contains("supervised"))
        {
            return "Older-child visits";
        }

        if (lower.Contains("family") || lower.Contains("children") || lower.Contains("kids"))
        {
            return "Child-specific notes";
        }

        if (lower.Contains("pet") || lower.Contains("cat") || lower.Contains("good with dogs"))
        {
            return "Pet-friendly";
        }

        if (lower.Contains("longer walks"))
        {
            return "Enjoys longer walks";
        }

        if (lower.Contains("outdoor play"))
        {
            return "Outdoor play";
        }

        if (lower.Contains("energetic") || lower.Contains("active"))
        {
            return "Active lifestyle fit";
        }

        return normalized;
    }

    private static bool IsShortReasonChip(string reason)
    {
        return reason.Length <= 34;
    }

    private string GetFavoriteIcon(int dogId)
    {
        return _favoriteDogIds.Contains(dogId)
            ? Icons.Material.Filled.Favorite
            : Icons.Material.Outlined.FavoriteBorder;
    }

    private Color GetFavoriteColor(int dogId)
    {
        return _favoriteDogIds.Contains(dogId) ? Color.Secondary : Color.Default;
    }

    private string GetFavoriteText(int dogId)
    {
        return _favoriteDogIds.Contains(dogId) ? "Saved" : "Save";
    }

    private static string GetDogDetailsUrl(int dogId)
    {
        return $"/dogs/{dogId}?returnUrl={Uri.EscapeDataString("/adopter/copilot")}";
    }
}
