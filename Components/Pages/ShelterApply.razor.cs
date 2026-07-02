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

public partial class ShelterApply
{
    [Inject] private IShelterRegistrationRequestService ShelterRegistrationRequestService { get; set; } = default!;
    [Inject] private IGeocodingService GeocodingService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private ShelterRegistrationRequest _request = new();
    private MudForm? _form;
    private bool _isSubmitting;
    private bool _isGeocoding;
    private bool _isReverseGeocoding;
    private bool _submitted;
    private ReverseGeocodingResult? _suggestedAddress;
    private bool _suggestedAddressUnavailable;
    private int _suggestedAddressRequestVersion;
    private bool _isCheckingAccess = true;
    private string? _currentUserId;
    private string? _blockedRole;
    private string? _formError;
    private string? _emailError;
    private string? _cityError;
    private string? _addressError;
    private string? _locationValidationError;

    private bool HasEmailError => !string.IsNullOrWhiteSpace(_emailError);
    private bool HasCityError => !string.IsNullOrWhiteSpace(_cityError);
    private bool HasAddressError => !string.IsNullOrWhiteSpace(_addressError);

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (authState.User.Identity?.IsAuthenticated == true)
        {
            var user = await UserManager.GetUserAsync(authState.User);
            _currentUserId = user?.Id;

            if (authState.User.IsInRole(IdentitySeedData.AdminRole))
            {
                _blockedRole = IdentitySeedData.AdminRole;
            }
            else if (authState.User.IsInRole(IdentitySeedData.ShelterRole))
            {
                _blockedRole = IdentitySeedData.ShelterRole;
            }
        }

        _isCheckingAccess = false;
    }

    private async Task FindCoordinatesAsync()
    {
        ClearAddressLookupErrors();

        if (string.IsNullOrWhiteSpace(_request.Address) || string.IsNullOrWhiteSpace(_request.City))
        {
            if (string.IsNullOrWhiteSpace(_request.City))
            {
                _cityError = "Enter city before searching for coordinates.";
            }

            if (string.IsNullOrWhiteSpace(_request.Address))
            {
                _addressError = "Enter address before searching for coordinates.";
            }

            return;
        }

        _isGeocoding = true;
        try
        {
            var result = await GeocodingService.FindCoordinatesAsync(_request.Address, _request.City);
            if (result is null)
            {
                Snackbar.Add("Could not find coordinates for this address. You can adjust the map manually or submit without map coordinates.", Severity.Warning);
                return;
            }

            _request.Latitude = result.Latitude;
            _request.Longitude = result.Longitude;
            _suggestedAddressRequestVersion++;
            _suggestedAddress = null;
            _suggestedAddressUnavailable = false;
            await LoadSuggestedAddressAsync(result.Latitude, result.Longitude);
            Snackbar.Add("Location found. You can adjust the pin if needed.", Severity.Success);
        }
        catch
        {
            Snackbar.Add("Could not search for coordinates right now. You can adjust the map manually or submit without a map location.", Severity.Error);
        }
        finally
        {
            _isGeocoding = false;
        }
    }

    private async Task OnMapCoordinatesChangedAsync((double Latitude, double Longitude) coordinates)
    {
        _request.Latitude = coordinates.Latitude;
        _request.Longitude = coordinates.Longitude;
        _locationValidationError = null;
        await LoadSuggestedAddressAsync(coordinates.Latitude, coordinates.Longitude);
    }

    private async Task LoadSuggestedAddressAsync(double latitude, double longitude)
    {
        var requestVersion = ++_suggestedAddressRequestVersion;
        _isReverseGeocoding = true;
        _suggestedAddress = null;
        _suggestedAddressUnavailable = false;

        try
        {
            var result = await GeocodingService.ReverseGeocodeAsync(latitude, longitude);
            if (requestVersion != _suggestedAddressRequestVersion)
            {
                return;
            }

            if (result is null)
            {
                _suggestedAddressUnavailable = true;
                return;
            }

            _suggestedAddress = result;
            FillNeighborhoodFromSuggestion(result);
        }
        catch
        {
            if (requestVersion != _suggestedAddressRequestVersion)
            {
                return;
            }

            _suggestedAddressUnavailable = true;
        }
        finally
        {
            if (requestVersion == _suggestedAddressRequestVersion)
            {
                _isReverseGeocoding = false;
            }
        }
    }

    private void UpdateAddressFromPin()
    {
        if (_suggestedAddress is null)
        {
            Snackbar.Add("No suggested address is available for this pin yet.", Severity.Info);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_suggestedAddress.SuggestedAddress))
        {
            _request.Address = _suggestedAddress.SuggestedAddress;
        }

        if (!string.IsNullOrWhiteSpace(_suggestedAddress.City))
        {
            _request.City = _suggestedAddress.City;
        }

        FillNeighborhoodFromSuggestion(_suggestedAddress, overwrite: true);
        _suggestedAddressRequestVersion++;
        _suggestedAddress = null;
        _suggestedAddressUnavailable = false;
        Snackbar.Add("Address updated from selected map location.", Severity.Success);
    }

    private static string GetSuggestedAddressPreview(ReverseGeocodingResult result)
    {
        return string.Join(", ", new[] { result.SuggestedAddress, result.Neighborhood, result.City }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct());
    }

    private void FillNeighborhoodFromSuggestion(ReverseGeocodingResult result, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(result.Neighborhood))
        {
            return;
        }

        if (overwrite || string.IsNullOrWhiteSpace(_request.Neighborhood))
        {
            _request.Neighborhood = result.Neighborhood.Trim();
        }
    }

    private async Task StartAnotherApplicationAsync()
    {
        _request = new ShelterRegistrationRequest();
        _suggestedAddressRequestVersion++;
        _suggestedAddress = null;
        _suggestedAddressUnavailable = false;
        ClearShelterApplicationValidationErrors();
        _submitted = false;
        if (_form is not null)
        {
            await _form.ResetValidationAsync();
        }
    }

    private async Task SubmitAsync()
    {
        ClearShelterApplicationValidationErrors();

        if (_form is not null)
        {
            await _form.ValidateAsync();
            if (!_form.IsValid)
            {
                return;
            }
        }

        _isSubmitting = true;
        try
        {
            await ShelterRegistrationRequestService.SubmitRequestAsync(_request, _currentUserId);
            _request = new();
            _suggestedAddressRequestVersion++;
            _suggestedAddress = null;
            _suggestedAddressUnavailable = false;
            _submitted = true;
            if (_form is not null)
            {
                await _form.ResetValidationAsync();
            }
            Snackbar.Add("Shelter application submitted successfully.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            if (!TryMapShelterApplicationValidationMessage(ex.Message))
            {
                _formError = ex.Message;
            }
        }
        catch
        {
            Snackbar.Add("Could not submit the shelter application. Please try again.", Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private void ClearShelterApplicationValidationErrors()
    {
        _formError = null;
        _emailError = null;
        ClearAddressLookupErrors();
        _locationValidationError = null;
    }

    private void ClearEmailValidationError()
    {
        _emailError = null;
        _formError = null;
    }

    private void ClearAddressLookupErrors()
    {
        _cityError = null;
        _addressError = null;
        _formError = null;
    }

    private bool TryMapShelterApplicationValidationMessage(string message)
    {
        switch (message)
        {
            case "A shelter application with this email is already pending review.":
            case "A shelter account with this email already exists.":
            case "A valid email address is required.":
                _emailError = message;
                return true;
            case "Latitude must be between -90 and 90.":
            case "Longitude must be between -180 and 180.":
                _locationValidationError = message;
                return true;
            default:
                return false;
        }
    }
}

