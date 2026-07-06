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

namespace PawConnect.Components.Pages;

public partial class Dogs
{
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private IGeocodingService GeocodingService { get; set; } = default!;
    [Inject] private IDistanceService DistanceService { get; set; } = default!;
    [Inject] private IFavoriteDogService FavoriteDogService { get; set; } = default!;
    [Inject] private ISavedDogSearchService SavedDogSearchService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<Dogs> Logger { get; set; } = default!;

    private List<Dog> _dogs = [];
    private List<PawConnect.Entities.Shelter> _shelters = [];
    private List<string> _breeds = [];
    private List<string> _coatColors = [];
    private List<string> _locations = [];
    private List<string> _neighborhoods = [];
    private Dictionary<int, double> _dogDistancesKm = [];
    private readonly HashSet<string> _unavailableImageUrls = new(StringComparer.OrdinalIgnoreCase);
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
    private GeocodingResult? _nearbyOrigin;
    private double? _nearestDogDistanceKm;
    private string _selectedBreed = string.Empty;
    private string _selectedCoatColor = string.Empty;
    private int? _maxAge;
    private int? _selectedShelterId;
    private int _selectedRadiusKm = 25;
    private string _selectedSize = string.Empty;
    private string _selectedLocation = string.Empty;
    private string _selectedNeighborhood = string.Empty;
    private string _selectedStatus = string.Empty;
    private string _selectedCatCompatibility = string.Empty;
    private string _selectedChildrenCompatibility = string.Empty;
    private string _selectedActivityLevel = string.Empty;
    private string _selectedApartmentSuitability = string.Empty;
    private DogSortOption _sortOption = DogSortOption.NameAsc;
    private string? _currentUserId;
    private bool _currentUserIsAuthenticated;
    private bool _currentUserIsAdopter;
    private HashSet<int> _favoriteDogIds = [];

    private static readonly int[] RadiusOptions = [5, 10, 25, 50, 100];

    private string ResultCountText => _dogs.Count == 1 ? "Showing 1 dog" : $"Showing {_dogs.Count} dogs";
    private IReadOnlyList<ActiveFilterChip> ActiveFilterChips => BuildActiveFilterChips();
    private int ActiveFilterCount => ActiveFilterChips.Count;
    private bool HasActiveFilters => ActiveFilterCount > 0;
    private string FilterButtonText => ActiveFilterCount == 0 ? "Filter by" : $"Filter by ({ActiveFilterCount})";
    private string CurrentSortLabel => GetDogSortLabel(_sortOption);
    private string EmptyStateTitle => _nearbyOrigin is not null
        ? $"No dogs found within {_selectedRadiusKm} km of this location."
        : _selectedShelterId.HasValue
        ? "No dogs found for the selected shelter."
        : "No dogs matched your filters";
    private string EmptyStateSubtitle => _nearbyOrigin is not null
        ? "Try increasing the radius or searching a different area."
        : _selectedShelterId.HasValue
        ? "Try another shelter or broaden your filters."
        : "Try clearing filters or broadening your search.";
    private string NearbyOriginLabel => _nearbyUsesBrowserLocation
        ? "your location"
        : _activeNearbyLabel ?? _nearbySearchTerm ?? "the selected location";
    private string NearbySummaryText => $"Showing dogs within {_selectedRadiusKm} km of {NearbyOriginLabel}.";
    private string? NearbyMatchedLocationText => !_nearbyUsesBrowserLocation &&
        !string.IsNullOrWhiteSpace(_activeNearbyDisplayName) &&
        !string.Equals(_activeNearbyDisplayName, _activeNearbyLabel, StringComparison.OrdinalIgnoreCase)
            ? $"Matched location: {_activeNearbyDisplayName}"
            : null;
    private string? NearestDogText => _nearbyOrigin is not null && _nearestDogDistanceKm.HasValue
        ? $"Nearest dog is {FormatDistance(_nearestDogDistanceKm.Value)} away."
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

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadCurrentUserAsync();
            var availableDogs = await DogService.GetAvailableDogsAsync();
            _shelters = await ShelterService.GetAllSheltersAsync();
            _dogs = availableDogs;
            _breeds = availableDogs.Select(DogBreedFormatter.Format).Distinct().OrderBy(b => b).ToList();
            _coatColors = DogCoatColorOptions.Values
                .Concat(availableDogs
                    .Select(d => DogCoatColorOptions.Normalize(d.CoatColor))
                    .Where(color => !string.IsNullOrWhiteSpace(color))
                    .Select(color => color!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(color => color)
                .ToList();
            _locations = availableDogs.Select(d => d.Location).Distinct().OrderBy(l => l).ToList();
            _neighborhoods = availableDogs
                .Select(d => d.Shelter?.Neighborhood)
                .Where(neighborhood => !string.IsNullOrWhiteSpace(neighborhood))
                .Select(neighborhood => neighborhood!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(neighborhood => neighborhood)
                .ToList();
            await LoadFavoriteStateAsync();
        }
        catch
        {
            _error = "Dog data could not be loaded. Apply migrations and update the database, then try again.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ApplyFiltersAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            var serviceSortOption = _sortOption == DogSortOption.NearestFirst
                ? DogSortOption.NameAsc
                : _sortOption;

            _dogs = await DogService.SearchDogsAsync(
                _searchTerm,
                EmptyToNull(_selectedBreed),
                _maxAge,
                ParseEnum<DogSize>(_selectedSize),
                EmptyToNull(_selectedLocation),
                ParsePublicStatus(_selectedStatus),
                serviceSortOption,
                _selectedShelterId,
                EmptyToNull(_selectedNeighborhood),
                EmptyToNull(_selectedCoatColor),
                ParseEnum<CatCompatibility>(_selectedCatCompatibility),
                ParseEnum<ChildrenCompatibility>(_selectedChildrenCompatibility),
                ParseEnum<DogActivityLevel>(_selectedActivityLevel),
                ParseEnum<ApartmentSuitability>(_selectedApartmentSuitability));
            _dogs = ApplyNearbyFilterAndSort(_dogs);
            await LoadFavoriteStateAsync();
        }
        catch
        {
            _error = "Dogs could not be filtered right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ApplyFiltersAndCloseAsync()
    {
        await ApplyFiltersAsync();
        _filtersOpen = false;
    }

    private async Task ClearFiltersAsync()
    {
        _searchTerm = null;
        _selectedBreed = string.Empty;
        _selectedCoatColor = string.Empty;
        _maxAge = null;
        _selectedShelterId = null;
        _selectedSize = string.Empty;
        _selectedLocation = string.Empty;
        _selectedNeighborhood = string.Empty;
        _selectedStatus = string.Empty;
        _selectedCatCompatibility = string.Empty;
        _selectedChildrenCompatibility = string.Empty;
        _selectedActivityLevel = string.Empty;
        _selectedApartmentSuitability = string.Empty;
        _sortOption = DogSortOption.NameAsc;
        _nearbySearchTerm = null;
        _nearbyError = null;
        _activeNearbyLabel = null;
        _activeNearbyDisplayName = null;
        _selectedNearbySuggestion = null;
        _nearbyOrigin = null;
        _nearbyUsesBrowserLocation = false;
        _selectedRadiusKm = 25;
        _dogDistancesKm = [];
        _nearestDogDistanceKm = null;
        await ApplyFiltersAsync();
        _filtersOpen = false;
    }

    private void ToggleFilters()
    {
        _filtersOpen = !_filtersOpen;
    }

    private async Task SetSortAsync(DogSortOption sortOption)
    {
        _sortOption = sortOption;
        await ApplyFiltersAsync();
    }

    private List<ActiveFilterChip> BuildActiveFilterChips()
    {
        var chips = new List<ActiveFilterChip>();

        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            chips.Add(new ActiveFilterChip($"Search: {ShortChipText(_searchTerm)}", ClearSearchFilterAsync));
        }

        if (_selectedShelterId.HasValue)
        {
            chips.Add(new ActiveFilterChip($"Shelter: {ShortChipText(GetSelectedShelterName() ?? "Selected shelter")}", ClearShelterFilterAsync));
        }

        if (!string.IsNullOrWhiteSpace(_selectedBreed))
        {
            chips.Add(new ActiveFilterChip($"Breed: {ShortChipText(_selectedBreed)}", ClearBreedFilterAsync));
        }

        if (!string.IsNullOrWhiteSpace(_selectedCoatColor))
        {
            chips.Add(new ActiveFilterChip($"Coat color: {ShortChipText(_selectedCoatColor)}", ClearCoatColorFilterAsync));
        }

        if (_maxAge.HasValue)
        {
            chips.Add(new ActiveFilterChip($"Max age: {_maxAge.Value}", ClearMaxAgeFilterAsync));
        }

        if (!string.IsNullOrWhiteSpace(_selectedSize))
        {
            chips.Add(new ActiveFilterChip($"Size: {_selectedSize}", ClearSizeFilterAsync));
        }

        if (!string.IsNullOrWhiteSpace(_selectedLocation))
        {
            chips.Add(new ActiveFilterChip($"Location: {ShortChipText(_selectedLocation)}", ClearLocationFilterAsync));
        }

        if (!string.IsNullOrWhiteSpace(_selectedNeighborhood))
        {
            chips.Add(new ActiveFilterChip($"Neighborhood: {ShortChipText(_selectedNeighborhood)}", ClearNeighborhoodFilterAsync));
        }

        if (!string.IsNullOrWhiteSpace(_selectedStatus))
        {
            chips.Add(new ActiveFilterChip($"Status: {_selectedStatus}", ClearStatusFilterAsync));
        }

        if (!string.IsNullOrWhiteSpace(_selectedCatCompatibility))
        {
            chips.Add(new ActiveFilterChip($"Cats: {DogCompatibilityFormatter.FormatCat(ParseEnum<CatCompatibility>(_selectedCatCompatibility) ?? CatCompatibility.Unknown)}", ClearCatCompatibilityFilterAsync));
        }

        if (!string.IsNullOrWhiteSpace(_selectedChildrenCompatibility))
        {
            chips.Add(new ActiveFilterChip($"Children: {DogCompatibilityFormatter.FormatChildren(ParseEnum<ChildrenCompatibility>(_selectedChildrenCompatibility) ?? ChildrenCompatibility.Unknown)}", ClearChildrenCompatibilityFilterAsync));
        }

        if (!string.IsNullOrWhiteSpace(_selectedActivityLevel))
        {
            chips.Add(new ActiveFilterChip($"Activity: {DogCompatibilityFormatter.FormatActivity(ParseEnum<DogActivityLevel>(_selectedActivityLevel) ?? DogActivityLevel.Unknown)}", ClearActivityLevelFilterAsync));
        }

        if (!string.IsNullOrWhiteSpace(_selectedApartmentSuitability))
        {
            chips.Add(new ActiveFilterChip($"Apartment: {DogCompatibilityFormatter.FormatApartment(ParseEnum<ApartmentSuitability>(_selectedApartmentSuitability) ?? ApartmentSuitability.Unknown)}", ClearApartmentSuitabilityFilterAsync));
        }

        if (_nearbyOrigin is not null)
        {
            chips.Add(new ActiveFilterChip($"Near: {ShortChipText(NearbyOriginLabel)}, {_selectedRadiusKm} km", ClearNearbyAsync));
        }

        return chips;
    }

    private async Task ClearSearchFilterAsync()
    {
        _searchTerm = null;
        await ApplyFiltersAsync();
    }

    private async Task ClearShelterFilterAsync()
    {
        _selectedShelterId = null;
        await ApplyFiltersAsync();
    }

    private async Task ClearBreedFilterAsync()
    {
        _selectedBreed = string.Empty;
        await ApplyFiltersAsync();
    }

    private async Task ClearCoatColorFilterAsync()
    {
        _selectedCoatColor = string.Empty;
        await ApplyFiltersAsync();
    }

    private async Task ClearMaxAgeFilterAsync()
    {
        _maxAge = null;
        await ApplyFiltersAsync();
    }

    private async Task ClearSizeFilterAsync()
    {
        _selectedSize = string.Empty;
        await ApplyFiltersAsync();
    }

    private async Task ClearLocationFilterAsync()
    {
        _selectedLocation = string.Empty;
        await ApplyFiltersAsync();
    }

    private async Task ClearNeighborhoodFilterAsync()
    {
        _selectedNeighborhood = string.Empty;
        await ApplyFiltersAsync();
    }

    private async Task ClearStatusFilterAsync()
    {
        _selectedStatus = string.Empty;
        await ApplyFiltersAsync();
    }

    private async Task ClearCatCompatibilityFilterAsync()
    {
        _selectedCatCompatibility = string.Empty;
        await ApplyFiltersAsync();
    }

    private async Task ClearChildrenCompatibilityFilterAsync()
    {
        _selectedChildrenCompatibility = string.Empty;
        await ApplyFiltersAsync();
    }

    private async Task ClearActivityLevelFilterAsync()
    {
        _selectedActivityLevel = string.Empty;
        await ApplyFiltersAsync();
    }

    private async Task ClearApartmentSuitabilityFilterAsync()
    {
        _selectedApartmentSuitability = string.Empty;
        await ApplyFiltersAsync();
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
                await ApplyFiltersAsync();
                return;
            }

            _nearbyOrigin = result;
            _activeNearbyLabel = selectedSuggestion is not null
                ? GetSuggestionDisplayText(selectedSuggestion)
                : _nearbySearchTerm.Trim();
            _activeNearbyDisplayName = result.DisplayName;
            _nearbyUsesBrowserLocation = false;
            Logger.LogDebug(
                "Nearby dog search input '{SearchInput}' matched '{DisplayName}' at {Latitude}, {Longitude}. Used selected suggestion: {UsedSuggestion}.",
                _nearbySearchTerm,
                result.DisplayName,
                result.Latitude,
                result.Longitude,
                selectedSuggestion is not null);
            await ApplyFiltersAsync();
        }
        catch
        {
            ClearNearbyOrigin();
            _nearbyError = "Could not find this location. Please try another city or address.";
            await ApplyFiltersAsync();
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
                    "Nearby dog search is using browser location at {Latitude}, {Longitude}.",
                    result.Latitude.Value,
                    result.Longitude.Value);
                await ApplyFiltersAsync();
                return;
            }

            ClearNearbyOrigin();
            _nearbyError = GetBrowserLocationErrorMessage(result.ErrorCode);
            await ApplyFiltersAsync();
        }
        catch (JSException)
        {
            ClearNearbyOrigin();
            _nearbyError = "Could not get your location. Please try again or search by city/address.";
            await ApplyFiltersAsync();
        }
        finally
        {
            _isUsingBrowserLocation = false;
        }
    }

    private async Task ClearNearbyAsync()
    {
        _nearbySearchTerm = null;
        _nearbyError = null;
        _selectedNearbySuggestion = null;
        ClearNearbyOrigin();
        if (_sortOption == DogSortOption.NearestFirst)
        {
            _sortOption = DogSortOption.NameAsc;
        }

        await ApplyFiltersAsync();
    }

    private void ClearNearbyOrigin()
    {
        _activeNearbyLabel = null;
        _activeNearbyDisplayName = null;
        _selectedNearbySuggestion = null;
        _nearbyOrigin = null;
        _nearbyUsesBrowserLocation = false;
        _dogDistancesKm = [];
        _nearestDogDistanceKm = null;
        if (_sortOption == DogSortOption.NearestFirst)
        {
            _sortOption = DogSortOption.NameAsc;
        }
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

    private async Task OnNearbyTextChanged(string text)
    {
        var hadNearbyOrigin = _nearbyOrigin is not null;
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

        if (hadNearbyOrigin && _nearbyOrigin is null)
        {
            await ApplyFiltersAsync();
        }
    }

    private async Task OnNearbySuggestionSelected(AddressSuggestion? suggestion)
    {
        _selectedNearbySuggestion = suggestion;
        if (suggestion is null)
        {
            if (_nearbyOrigin is not null)
            {
                ClearNearbyOrigin();
                await ApplyFiltersAsync();
            }

            return;
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
                "Nearby dog autocomplete suggestion '{DisplayName}' selected at {Latitude}, {Longitude}.",
                suggestion.DisplayName,
                suggestion.Latitude,
                suggestion.Longitude);
            await ApplyFiltersAsync();
        }
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

    private List<Dog> ApplyNearbyFilterAndSort(List<Dog> dogs)
    {
        _dogDistancesKm = [];
        _nearestDogDistanceKm = null;
        if (_nearbyOrigin is null)
        {
            return dogs;
        }

        var filteredDogs = new List<Dog>();
        Logger.LogDebug(
            "Applying nearby dog filter from '{OriginLabel}' at {Latitude}, {Longitude} with radius {RadiusKm} km.",
            NearbyOriginLabel,
            _nearbyOrigin.Latitude,
            _nearbyOrigin.Longitude,
            _selectedRadiusKm);

        foreach (var dog in dogs)
        {
            if (!TryGetDogDistanceKm(dog, out var distance))
            {
                Logger.LogDebug(
                    "Nearby dog filter excluded dog '{DogName}' because shelter '{ShelterName}' has no coordinates.",
                    dog.Name,
                    dog.Shelter?.Name ?? "Unknown shelter");
                continue;
            }

            if (!_nearestDogDistanceKm.HasValue || distance < _nearestDogDistanceKm.Value)
            {
                _nearestDogDistanceKm = distance;
            }

            var isIncluded = distance <= _selectedRadiusKm;
            Logger.LogDebug(
                "Nearby dog filter shelter '{ShelterName}' at {ShelterLatitude}, {ShelterLongitude}: distance {DistanceKm} km, included {Included}.",
                dog.Shelter?.Name ?? "Unknown shelter",
                dog.Shelter?.Latitude,
                dog.Shelter?.Longitude,
                Math.Round(distance, 2),
                isIncluded);

            if (isIncluded)
            {
                filteredDogs.Add(dog);
            }
        }

        if (_sortOption == DogSortOption.NearestFirst)
        {
            filteredDogs = filteredDogs
                .OrderBy(dog => _dogDistancesKm[dog.Id])
                .ThenBy(dog => dog.Name)
                .ToList();
        }

        return filteredDogs;
    }

    private async Task IncreaseNearbyRadiusAsync(int radiusKm)
    {
        _selectedRadiusKm = radiusKm;
        await ApplyFiltersAsync();
    }

    private async Task OnRadiusChangedAsync(int radiusKm)
    {
        _selectedRadiusKm = radiusKm;
        if (_nearbyOrigin is not null)
        {
            await ApplyFiltersAsync();
        }
    }

    private bool TryGetDogDistanceKm(Dog dog, out double distance)
    {
        distance = 0;
        if (_nearbyOrigin is null ||
            dog.Shelter?.Latitude is null ||
            dog.Shelter.Longitude is null)
        {
            return false;
        }

        distance = DistanceService.CalculateDistanceKm(
            _nearbyOrigin.Latitude,
            _nearbyOrigin.Longitude,
            dog.Shelter.Latitude.Value,
            dog.Shelter.Longitude.Value);
        _dogDistancesKm[dog.Id] = distance;
        return true;
    }

    private string? GetMainImageUrl(Dog dog)
    {
        return DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images, _unavailableImageUrls);
    }

    private void MarkImageUnavailable(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        _unavailableImageUrls.Add(imageUrl.Trim());
    }

    private static string GetDogImageAlt(Dog dog)
    {
        return $"Photo of {dog.Name}, {DogBreedFormatter.Format(dog)}";
    }

    private static string GetShortDescription(string description)
    {
        return description.Length <= 120 ? description : $"{description[..117]}...";
    }

    private static string? GetShelterLine(Dog dog)
    {
        var shelterName = dog.Shelter?.Name?.Trim();
        var shelterNeighborhood = dog.Shelter?.Neighborhood?.Trim();
        var shelterCity = dog.Shelter?.City?.Trim();
        var shelterLocation = string.Join(", ", new[] { shelterNeighborhood, shelterCity }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(shelterName) && !string.IsNullOrWhiteSpace(shelterLocation))
        {
            return $"{shelterName} Â· {shelterLocation}";
        }

        return (string.IsNullOrWhiteSpace(shelterName), string.IsNullOrWhiteSpace(shelterLocation)) switch
        {
            (false, true) => shelterName,
            (true, false) => shelterLocation,
            _ => null
        };
    }

    private string? GetDistanceText(Dog dog)
    {
        return _dogDistancesKm.TryGetValue(dog.Id, out var distance)
            ? $"{FormatDistance(distance)} away"
            : null;
    }

    private static string FormatDistance(double distanceKm)
    {
        return $"{distanceKm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} km";
    }

    private string? GetSelectedShelterName()
    {
        return _shelters.FirstOrDefault(shelter => shelter.Id == _selectedShelterId)?.Name;
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

    private static string GetDogSortLabel(DogSortOption sortOption)
    {
        return sortOption switch
        {
            DogSortOption.NameDesc => "Name Z-A",
            DogSortOption.AgeAsc => "Age ascending",
            DogSortOption.AgeDesc => "Age descending",
            DogSortOption.BreedAsc => "Breed A-Z",
            DogSortOption.LocationAsc => "Location A-Z",
            DogSortOption.Status => "Status",
            DogSortOption.NewestFirst => "Newest first",
            DogSortOption.NearestFirst => "Nearest first",
            _ => "Name A-Z"
        };
    }

    private static Color GetStatusColor(DogStatus status)
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

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static TEnum? ParseEnum<TEnum>(string value) where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, out var result) ? result : null;
    }

    private static DogStatus? ParsePublicStatus(string value)
    {
        var status = ParseEnum<DogStatus>(value);
        return status is DogStatus.Available or DogStatus.Reserved ? status : null;
    }

    private async Task LoadCurrentUserAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var principal = authState.User;

        _currentUserIsAuthenticated = principal.Identity?.IsAuthenticated == true;
        _currentUserIsAdopter = _currentUserIsAuthenticated && principal.IsInRole("Adopter");
        if (!_currentUserIsAdopter)
        {
            _currentUserId = null;
            return;
        }

        var user = await UserManager.GetUserAsync(principal);
        _currentUserId = user?.Id;
    }

    private async Task LoadFavoriteStateAsync()
    {
        if (!_currentUserIsAdopter || string.IsNullOrWhiteSpace(_currentUserId))
        {
            _favoriteDogIds = [];
            return;
        }

        _favoriteDogIds = await FavoriteDogService.GetFavoriteDogIdsForUserAsync(_currentUserId);
    }

    private async Task ToggleFavoriteAsync(Dog dog)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Please log in with an adopter account to save favorite dogs.", Severity.Info);
            NavigationManager.NavigateTo($"/Account/Login?returnUrl={Uri.EscapeDataString(NavigationManager.Uri)}");
            return;
        }

        if (!_currentUserIsAdopter)
        {
            Snackbar.Add("Only adopter accounts can save favorite dogs.", Severity.Warning);
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

    private string GetFavoriteTitle(int dogId)
    {
        return _favoriteDogIds.Contains(dogId) ? "Remove from favorites" : "Add to favorites";
    }
}

