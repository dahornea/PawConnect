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

public partial class Shelters
{
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private IGeocodingService GeocodingService { get; set; } = default!;
    [Inject] private IDistanceService DistanceService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<Shelters> Logger { get; set; } = default!;

private List<PawConnect.Entities.Shelter> _shelters = [];
    private bool _isLoading = true;
    private bool _isFindingNearby;
    private bool _isUsingBrowserLocation;
    private bool _nearbyUsesBrowserLocation;
    private bool _filtersOpen;
    private string? _error;
    private string? _searchTerm;
    private string? _nearbySearchTerm;
    private string? _nearbyError;
    private string? _activeNearbyLabel;
    private string? _activeNearbyDisplayName;
    private AddressSuggestion? _selectedNearbySuggestion;
    private double? _nearestShelterDistanceKm;
    private int _selectedRadiusKm = 25;
    private GeocodingResult? _nearbyOrigin;
    private ShelterSortOption _sortOption = ShelterSortOption.NameAsc;

    private static readonly int[] RadiusOptions = [5, 10, 25, 50, 100];
    private IReadOnlyList<ActiveFilterChip> ActiveFilterChips => BuildActiveFilterChips();
    private int ActiveFilterCount => ActiveFilterChips.Count;
    private bool HasActiveFilters => ActiveFilterCount > 0;
    private string FilterButtonText => ActiveFilterCount == 0 ? "Filter by" : $"Filter by ({ActiveFilterCount})";
    private string CurrentSortLabel => GetShelterSortLabel(_sortOption);
    private string NearbyOriginLabel => _nearbyUsesBrowserLocation
        ? "your location"
        : _activeNearbyLabel ?? _nearbySearchTerm ?? "the selected location";
    private string NearbySummaryText => $"Showing shelters within {_selectedRadiusKm} km of {NearbyOriginLabel}.";
    private string? NearbyMatchedLocationText => !_nearbyUsesBrowserLocation &&
        !string.IsNullOrWhiteSpace(_activeNearbyDisplayName) &&
        !string.Equals(_activeNearbyDisplayName, _activeNearbyLabel, StringComparison.OrdinalIgnoreCase)
            ? $"Matched location: {_activeNearbyDisplayName}"
            : null;
    private string EmptyStateTitle => _nearbyOrigin is not null
        ? $"No shelters found within {_selectedRadiusKm} km of this location."
        : "No shelters match your search.";
    private string EmptyStateSubtitle => _nearbyOrigin is not null
        ? "Try increasing the radius or searching a different area."
        : "Try searching by shelter name, city, or street address.";
    private string? NearestShelterText => _nearbyOrigin is not null && _nearestShelterDistanceKm.HasValue
        ? $"Nearest shelter is {FormatDistance(_nearestShelterDistanceKm.Value)} away."
        : null;
    private bool HasManualNearbyText => !string.IsNullOrWhiteSpace(_nearbySearchTerm) &&
        _nearbySearchTerm.Trim().Length >= 3 &&
        _selectedNearbySuggestion is null &&
        _nearbyOrigin is null;
    private int? SuggestedRadiusKm
    {
        get
        {
            var nextRadius = _selectedRadiusKm < 25
                ? 25
                : RadiusOptions.FirstOrDefault(radius => radius > _selectedRadiusKm);
            return nextRadius == 0 ? null : nextRadius;
        }
    }

    private sealed record ActiveFilterChip(string Label, Func<Task> ClearAsync);

    private IEnumerable<PawConnect.Entities.Shelter> GetFilteredShelters()
    {
        _nearestShelterDistanceKm = null;
        IEnumerable<PawConnect.Entities.Shelter> query = _shelters;

        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            var term = _searchTerm.Trim();
            query = query.Where(shelter =>
                Contains(shelter.Name, term) ||
                Contains(shelter.Neighborhood, term) ||
                Contains(shelter.City, term) ||
                Contains(shelter.Address, term));
        }

        if (_nearbyOrigin is not null)
        {
            var sheltersWithDistance = new List<(PawConnect.Entities.Shelter Shelter, double Distance)>();
            Logger.LogDebug(
                "Applying nearby shelter filter from '{OriginLabel}' at {Latitude}, {Longitude} with radius {RadiusKm} km.",
                NearbyOriginLabel,
                _nearbyOrigin.Latitude,
                _nearbyOrigin.Longitude,
                _selectedRadiusKm);

            foreach (var shelter in query)
            {
                var distance = GetShelterDistanceKm(shelter);
                if (!distance.HasValue)
                {
                    Logger.LogDebug(
                        "Nearby shelter filter excluded shelter '{ShelterName}' because it has no coordinates.",
                        shelter.Name);
                    continue;
                }

                if (!_nearestShelterDistanceKm.HasValue || distance.Value < _nearestShelterDistanceKm.Value)
                {
                    _nearestShelterDistanceKm = distance.Value;
                }

                var isIncluded = distance.Value <= _selectedRadiusKm;
                Logger.LogDebug(
                    "Nearby shelter filter shelter '{ShelterName}' at {ShelterLatitude}, {ShelterLongitude}: distance {DistanceKm} km, included {Included}.",
                    shelter.Name,
                    shelter.Latitude,
                    shelter.Longitude,
                    Math.Round(distance.Value, 2),
                    isIncluded);

                if (isIncluded)
                {
                    sheltersWithDistance.Add((shelter, distance.Value));
                }
            }

            return _sortOption == ShelterSortOption.NearestFirst
                ? sheltersWithDistance
                    .OrderBy(item => item.Distance)
                    .ThenBy(item => item.Shelter.Name)
                    .Select(item => item.Shelter)
                : SortShelters(sheltersWithDistance.Select(item => item.Shelter));
        }

        return SortShelters(query);
    }

    private IEnumerable<PawConnect.Entities.Shelter> SortShelters(IEnumerable<PawConnect.Entities.Shelter> shelters)
    {
        return _sortOption switch
        {
            ShelterSortOption.NameDesc => shelters.OrderByDescending(shelter => shelter.Name),
            ShelterSortOption.CityAsc => shelters.OrderBy(shelter => shelter.City).ThenBy(shelter => shelter.Name),
            _ => shelters.OrderBy(shelter => shelter.Name)
        };
    }

    private void ToggleFilters()
    {
        _filtersOpen = !_filtersOpen;
    }

    private void SetSort(ShelterSortOption sortOption)
    {
        _sortOption = sortOption;
    }

    private Task ApplySearchFilterAsync()
    {
        _filtersOpen = false;
        return Task.CompletedTask;
    }

    private List<ActiveFilterChip> BuildActiveFilterChips()
    {
        var chips = new List<ActiveFilterChip>();

        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            chips.Add(new ActiveFilterChip($"Search: {ShortChipText(_searchTerm)}", ClearSearchFilterAsync));
        }

        if (_nearbyOrigin is not null)
        {
            chips.Add(new ActiveFilterChip($"Near: {ShortChipText(NearbyOriginLabel)}, {_selectedRadiusKm} km", ClearNearbyAsync));
        }

        return chips;
    }

    private Task ClearSearchFilterAsync()
    {
        _searchTerm = null;
        return Task.CompletedTask;
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _shelters = await ShelterService.GetAllSheltersAsync();
        }
        catch
        {
            _error = "Shelters could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static bool Contains(string? value, string term)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private async Task FindNearbyAsync()
    {
        if (string.IsNullOrWhiteSpace(_nearbySearchTerm))
        {
            _nearbyError = "Enter a city or address to search nearby.";
            return;
        }

        _isFindingNearby = true;
        _nearbyError = null;

        try
        {
            var selectedSuggestion = GetCurrentSelectedSuggestion();
            var result = selectedSuggestion is not null
                ? new GeocodingResult(selectedSuggestion.Latitude, selectedSuggestion.Longitude, selectedSuggestion.DisplayName)
                : await GeocodingService.FindCoordinatesAsync(_nearbySearchTerm);
            if (result is null)
            {
                ClearNearbyOrigin();
                _nearbyError = "Could not find this location. Please try another city or address.";
                return;
            }

            _nearbyOrigin = result;
            _activeNearbyLabel = selectedSuggestion is not null
                ? GetSuggestionDisplayText(selectedSuggestion)
                : _nearbySearchTerm.Trim();
            _activeNearbyDisplayName = result.DisplayName;
            _nearbyUsesBrowserLocation = false;
            Logger.LogDebug(
                "Nearby shelter search input '{SearchInput}' matched '{DisplayName}' at {Latitude}, {Longitude}. Used selected suggestion: {UsedSuggestion}.",
                _nearbySearchTerm,
                result.DisplayName,
                result.Latitude,
                result.Longitude,
                selectedSuggestion is not null);
        }
        catch
        {
            ClearNearbyOrigin();
            _nearbyError = "Could not find this location. Please try another city or address.";
        }
        finally
        {
            _isFindingNearby = false;
        }
    }

    private async Task UseMyLocationAsync()
    {
        _isUsingBrowserLocation = true;
        _nearbyError = null;

        try
        {
            var result = await JSRuntime.InvokeAsync<BrowserLocationResult>("pawConnect.getCurrentLocation");
            if (result.Success && result.Latitude.HasValue && result.Longitude.HasValue)
            {
                _nearbyOrigin = new GeocodingResult(result.Latitude.Value, result.Longitude.Value, "Your location");
                _nearbySearchTerm = null;
                _activeNearbyLabel = null;
                _activeNearbyDisplayName = null;
                _selectedNearbySuggestion = null;
                _nearbyUsesBrowserLocation = true;
                Logger.LogDebug(
                    "Nearby shelter search is using browser location at {Latitude}, {Longitude}.",
                    result.Latitude.Value,
                    result.Longitude.Value);
                return;
            }

            ClearNearbyOrigin();
            _nearbyError = GetBrowserLocationErrorMessage(result.ErrorCode);
        }
        catch (JSException)
        {
            ClearNearbyOrigin();
            _nearbyError = "Could not get your location. Please try again or search by city/address.";
        }
        finally
        {
            _isUsingBrowserLocation = false;
        }
    }

    private void ClearFilters()
    {
        _searchTerm = null;
        _nearbySearchTerm = null;
        _nearbyError = null;
        ClearNearbyOrigin();
        _selectedRadiusKm = 25;
        _sortOption = ShelterSortOption.NameAsc;
    }

    private Task ClearFiltersAsync()
    {
        ClearFilters();
        _filtersOpen = false;
        return Task.CompletedTask;
    }

    private void ClearNearbyOrigin()
    {
        _activeNearbyLabel = null;
        _activeNearbyDisplayName = null;
        _selectedNearbySuggestion = null;
        _nearbyOrigin = null;
        _nearbyUsesBrowserLocation = false;
        _nearestShelterDistanceKm = null;
        if (_sortOption == ShelterSortOption.NearestFirst)
        {
            _sortOption = ShelterSortOption.NameAsc;
        }
    }

    private Task ClearNearbyAsync()
    {
        _nearbySearchTerm = null;
        _nearbyError = null;
        ClearNearbyOrigin();
        return Task.CompletedTask;
    }

    private Task IncreaseNearbyRadiusAsync(int radiusKm)
    {
        _selectedRadiusKm = radiusKm;
        return Task.CompletedTask;
    }

    private Task OnRadiusChangedAsync(int radiusKm)
    {
        _selectedRadiusKm = radiusKm;
        return Task.CompletedTask;
    }

    private static string GetBrowserLocationErrorMessage(string? errorCode)
    {
        return errorCode switch
        {
            "permission-denied" => "Location permission was denied. You can still search by city or address.",
            "unsupported" => "Your browser does not support location access. Please search by city or address.",
            "position-unavailable" or "timeout" => "Could not get your location. Please try again or search by city/address.",
            _ => "Could not get your location. Please try again or search by city/address."
        };
    }

    private async Task<IEnumerable<AddressSuggestion>> SearchNearbySuggestionsAsync(string? query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
        {
            return [];
        }

        return await GeocodingService.SearchAddressSuggestionsAsync(query, 5, cancellationToken);
    }

    private Task OnNearbyTextChanged(string text)
    {
        _nearbySearchTerm = text;
        if (_nearbyOrigin is not null &&
            (_selectedNearbySuggestion is null || !IsSelectedSuggestionText(text, _selectedNearbySuggestion)))
        {
            ClearNearbyOrigin();
        }
        else if (_selectedNearbySuggestion is not null &&
            !IsSelectedSuggestionText(text, _selectedNearbySuggestion))
        {
            ClearNearbyOrigin();
        }

        return Task.CompletedTask;
    }

    private Task OnNearbySuggestionSelected(AddressSuggestion? suggestion)
    {
        _selectedNearbySuggestion = suggestion;
        if (suggestion is null)
        {
            if (_nearbyOrigin is not null)
            {
                ClearNearbyOrigin();
            }

            return Task.CompletedTask;
        }

        if (suggestion is not null)
        {
            var label = GetSuggestionDisplayText(suggestion);
            _nearbySearchTerm = label;
            _nearbyError = null;
            _nearbyOrigin = new GeocodingResult(suggestion.Latitude, suggestion.Longitude, suggestion.DisplayName);
            _activeNearbyLabel = label;
            _activeNearbyDisplayName = suggestion.DisplayName;
            _nearbyUsesBrowserLocation = false;
            Logger.LogDebug(
                "Nearby shelter autocomplete suggestion '{DisplayName}' selected at {Latitude}, {Longitude}.",
                suggestion.DisplayName,
                suggestion.Latitude,
                suggestion.Longitude);
        }

        return Task.CompletedTask;
    }

    private AddressSuggestion? GetCurrentSelectedSuggestion()
    {
        return _selectedNearbySuggestion is not null &&
            IsSelectedSuggestionText(_nearbySearchTerm, _selectedNearbySuggestion)
                ? _selectedNearbySuggestion
                : null;
    }

    private static bool IsSelectedSuggestionText(string? text, AddressSuggestion suggestion)
    {
        return string.Equals(text?.Trim(), suggestion.DisplayName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text?.Trim(), GetSuggestionDisplayText(suggestion), StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPublicDogCount(PawConnect.Entities.Shelter shelter)
    {
        return shelter.Dogs.Count(dog => dog.Status is PawConnect.Entities.DogStatus.Available or PawConnect.Entities.DogStatus.Reserved);
    }

    private static string GetDogCountText(int publicDogCount)
    {
        return publicDogCount switch
        {
            0 => "No public dogs yet",
            1 => "1 public dog",
            _ => $"{publicDogCount} public dogs"
        };
    }

    private static bool HasMapLocation(PawConnect.Entities.Shelter shelter)
    {
        return shelter.Latitude.HasValue && shelter.Longitude.HasValue;
    }

    private static string GetShelterLocationLine(PawConnect.Entities.Shelter shelter)
    {
        return string.Join(", ", new[] { shelter.Neighborhood, shelter.City }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private bool TryGetShelterDistanceKm(PawConnect.Entities.Shelter shelter, out double distance)
    {
        distance = 0;
        var calculatedDistance = GetShelterDistanceKm(shelter);
        if (!calculatedDistance.HasValue)
        {
            return false;
        }

        distance = calculatedDistance.Value;
        return true;
    }

    private double? GetShelterDistanceKm(PawConnect.Entities.Shelter shelter)
    {
        if (_nearbyOrigin is null ||
            shelter.Latitude is null ||
            shelter.Longitude is null)
        {
            return null;
        }

        return DistanceService.CalculateDistanceKm(
            _nearbyOrigin.Latitude,
            _nearbyOrigin.Longitude,
            shelter.Latitude.Value,
            shelter.Longitude.Value);
    }

    private string? GetDistanceText(PawConnect.Entities.Shelter shelter)
    {
        var distance = GetShelterDistanceKm(shelter);
        return distance.HasValue
            ? $"{FormatDistance(distance.Value)} away"
            : null;
    }

    private static string FormatDistance(double distanceKm)
    {
        return $"{distanceKm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} km";
    }

    private static string ShortChipText(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= 34 ? text : $"{text[..31]}...";
    }

    private static string GetSuggestionDisplayText(AddressSuggestion? suggestion)
    {
        if (suggestion is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(suggestion.Address) &&
            !string.IsNullOrWhiteSpace(suggestion.City))
        {
            return $"{suggestion.Address}, {suggestion.City}";
        }

        return !string.IsNullOrWhiteSpace(suggestion.City)
            ? suggestion.City
            : suggestion.DisplayName;
    }

    private static string GetShelterSortLabel(ShelterSortOption sortOption)
    {
        return sortOption switch
        {
            ShelterSortOption.NameDesc => "Name Z-A",
            ShelterSortOption.CityAsc => "City A-Z",
            ShelterSortOption.NearestFirst => "Nearest first",
            _ => "Name A-Z"
        };
    }

    private static string GetShelterResultCountText(int count)
    {
        return count == 1 ? "Showing 1 shelter" : $"Showing {count} shelters";
    }

    private enum ShelterSortOption
    {
        NameAsc,
        NameDesc,
        CityAsc,
        NearestFirst
    }
}

