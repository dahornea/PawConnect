using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
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
using ShelterEntity = PawConnect.Entities.Shelter;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminShelters
{
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private IGeocodingService GeocodingService { get; set; } = default!;
    [Inject] private IExportService ExportService { get; set; } = default!;
    [Inject] private ICsvImportService CsvImportService { get; set; } = default!;
    [Inject] private IBrowserFileDownloadService FileDownloadService { get; set; } = default!;
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private List<ShelterEntity> _shelters = [];
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isGeocoding;
    private bool _isReverseGeocoding;
    private bool _isExporting;
    private bool _isImporting;
    private string? _error;
    private ShelterEntity? _editShelter;
    private IBrowserFile? _shelterRequestImportFile;
    private CsvImportResult? _importResult;
    private int _shelterRequestImportInputKey;
    private double? _savedLatitude;
    private double? _savedLongitude;
    private string _savedAddress = string.Empty;
    private string _savedCity = string.Empty;
    private string _savedNeighborhood = string.Empty;
    private string? _shelterFormError;
    private string? _shelterEmailError;
    private string? _shelterLocationError;
    private string? _visitHoursError;
    private ReverseGeocodingResult? _suggestedAddress;
    private bool _suggestedAddressUnavailable;
    private int _suggestedAddressRequestVersion;
    private MudForm? _shelterForm;
    private const long MaxCsvFileSize = 2 * 1024 * 1024;
    private bool HasShelterEmailError => !string.IsNullOrWhiteSpace(_shelterEmailError);
    private bool IsLocationDirty => _editShelter is not null && LocationDiffersFromSaved(_editShelter);

    protected override async Task OnInitializedAsync()
    {
        await LoadSheltersAsync();
    }

    private async Task LoadSheltersAsync()
    {
        try
        {
            _shelters = await ShelterService.GetAllSheltersAsync();
        }
        catch
        {
            _error = "Shelter data could not be loaded. Check migrations and database connection.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void EditShelter(ShelterEntity shelter)
    {
        _editShelter = new ShelterEntity
        {
            Id = shelter.Id,
            Name = shelter.Name,
            Description = shelter.Description,
            Address = shelter.Address,
            City = shelter.City,
            PhoneNumber = shelter.PhoneNumber,
            Email = shelter.Email,
            Neighborhood = shelter.Neighborhood,
            Latitude = shelter.Latitude,
            Longitude = shelter.Longitude,
            VisitStartTime = shelter.VisitStartTime,
            VisitEndTime = shelter.VisitEndTime,
            VisitsAllowedMonday = shelter.VisitsAllowedMonday,
            VisitsAllowedTuesday = shelter.VisitsAllowedTuesday,
            VisitsAllowedWednesday = shelter.VisitsAllowedWednesday,
            VisitsAllowedThursday = shelter.VisitsAllowedThursday,
            VisitsAllowedFriday = shelter.VisitsAllowedFriday,
            VisitsAllowedSaturday = shelter.VisitsAllowedSaturday,
            VisitsAllowedSunday = shelter.VisitsAllowedSunday,
            DogCapacity = shelter.DogCapacity,
            ReservedEmergencySpaces = shelter.ReservedEmergencySpaces
        };
        _savedLatitude = shelter.Latitude;
        _savedLongitude = shelter.Longitude;
        _savedAddress = shelter.Address ?? string.Empty;
        _savedCity = shelter.City ?? string.Empty;
        _savedNeighborhood = shelter.Neighborhood ?? string.Empty;
        ClearShelterEditValidationErrors();
        _suggestedAddressRequestVersion++;
        _suggestedAddress = null;
        _suggestedAddressUnavailable = false;
    }

    private void CancelEdit()
    {
        _editShelter = null;
        _savedLatitude = null;
        _savedLongitude = null;
        _savedAddress = string.Empty;
        _savedCity = string.Empty;
        _savedNeighborhood = string.Empty;
        ClearShelterEditValidationErrors();
        _suggestedAddressRequestVersion++;
        _suggestedAddress = null;
        _suggestedAddressUnavailable = false;
    }

    private async Task ExportSheltersCsvAsync()
    {
        await ExportAsync(() => ExportService.GenerateSheltersCsvAsync());
    }

    private async Task OnShelterRequestCsvSelectedAsync(InputFileChangeEventArgs args)
    {
        _shelterRequestImportFile = args.File;
        _importResult = null;

        if (!IsCsvFile(_shelterRequestImportFile))
        {
            Snackbar.Add("Please choose a .csv file.", Severity.Warning);
            ClearShelterRequestImportPreview();
            return;
        }

        if (_shelterRequestImportFile.Size > MaxCsvFileSize)
        {
            Snackbar.Add("CSV file is too large. Please use a file under 2 MB.", Severity.Warning);
            ClearShelterRequestImportPreview();
            return;
        }

        Snackbar.Add("CSV selected. Click Preview Import to validate it.", Severity.Info);
        await Task.CompletedTask;
    }

    private async Task PreviewSelectedShelterRequestFileAsync()
    {
        if (_shelterRequestImportFile is null)
        {
            Snackbar.Add("Choose a shelter request CSV file first.", Severity.Info);
            return;
        }

        if (!IsCsvFile(_shelterRequestImportFile))
        {
            Snackbar.Add("Please choose a .csv file.", Severity.Warning);
            return;
        }

        if (_shelterRequestImportFile.Size > MaxCsvFileSize)
        {
            Snackbar.Add("CSV file is too large. Please use a file under 2 MB.", Severity.Warning);
            return;
        }

        _isImporting = true;

        try
        {
            await using var stream = _shelterRequestImportFile.OpenReadStream(MaxCsvFileSize);
            _importResult = await CsvImportService.PreviewAdminShelterRequestsImportAsync(stream);
            Snackbar.Add(_importResult.HasErrors
                ? "CSV contains validation errors. Please review the rows below."
                : "Shelter request CSV preview is ready.",
                _importResult.HasErrors ? Severity.Warning : Severity.Success);
        }
        catch
        {
            Snackbar.Add("CSV could not be previewed. Please check the file and try again.", Severity.Error);
        }
        finally
        {
            _isImporting = false;
        }
    }

    private async Task ConfirmShelterRequestImportAsync()
    {
        if (_shelterRequestImportFile is null)
        {
            Snackbar.Add("Choose a shelter request CSV file first.", Severity.Info);
            return;
        }

        _isImporting = true;

        try
        {
            await using var stream = _shelterRequestImportFile.OpenReadStream(MaxCsvFileSize);
            _importResult = await CsvImportService.ImportAdminShelterRequestsAsync(stream);
            if (_importResult.HasErrors)
            {
                Snackbar.Add("CSV contains validation errors. Please review the rows below.", Severity.Warning);
                return;
            }

            Snackbar.Add("Shelter requests imported successfully.", Severity.Success);
            ClearShelterRequestImportPreview();
        }
        catch
        {
            Snackbar.Add("CSV could not be imported. Please review the validation errors.", Severity.Error);
        }
        finally
        {
            _isImporting = false;
        }
    }

    private async Task DownloadShelterRequestsTemplateAsync()
    {
        await FileDownloadService.DownloadAsync(CsvImportService.GenerateAdminShelterRequestsTemplate());
    }

    private void ClearShelterRequestImportPreview()
    {
        _importResult = null;
        _shelterRequestImportFile = null;
        _shelterRequestImportInputKey++;
    }

    private async Task ExportAsync(Func<Task<ExportFile>> exportAction)
    {
        _isExporting = true;

        try
        {
            var file = await exportAction();
            await FileDownloadService.DownloadAsync(file);
            Snackbar.Add("Export generated successfully.", Severity.Success);
        }
        catch
        {
            Snackbar.Add("Could not generate export. Please try again.", Severity.Error);
        }
        finally
        {
            _isExporting = false;
        }
    }

    private async Task SaveShelterAsync()
    {
        if (_editShelter is null)
        {
            return;
        }

        if (_shelterForm is not null)
        {
            await _shelterForm.ValidateAsync();
            if (!_shelterForm.IsValid)
            {
                return;
            }
        }

        ClearShelterEditValidationErrors();
        _isSaving = true;

        try
        {
            await ShelterService.UpdateShelterProfileAsync(_editShelter);
            await AuditLogService.LogAsync(
                AuditActions.ShelterUpdatedByAdmin,
                "Shelter",
                _editShelter.Id.ToString(),
                $"Shelter profile/contact information was updated by an admin for {_editShelter.Name}.");
            Snackbar.Add("Shelter profile saved.", Severity.Success);
            _editShelter = null;
            _savedLatitude = null;
            _savedLongitude = null;
            _savedAddress = string.Empty;
            _savedCity = string.Empty;
            _savedNeighborhood = string.Empty;
            _suggestedAddressRequestVersion++;
            _suggestedAddress = null;
            _suggestedAddressUnavailable = false;
            await LoadSheltersAsync();
        }
        catch (InvalidOperationException ex)
        {
            if (!TryMapShelterEditValidationMessage(ex.Message))
            {
                _shelterFormError = ex.Message;
            }
        }
        catch
        {
            Snackbar.Add("Could not save shelter profile. Please try again.", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task OnMapCoordinatesChangedAsync((double Latitude, double Longitude) coordinates)
    {
        if (_editShelter is null)
        {
            return;
        }

        _editShelter.Latitude = coordinates.Latitude;
        _editShelter.Longitude = coordinates.Longitude;
        _shelterLocationError = null;
        if (!LocationDiffersFromSaved(_editShelter))
        {
            _suggestedAddressRequestVersion++;
            _suggestedAddress = null;
            _suggestedAddressUnavailable = false;
            return;
        }

        await LoadSuggestedAddressAsync(coordinates.Latitude, coordinates.Longitude);
    }

    private void RevertToSavedLocation()
    {
        if (_editShelter is null)
        {
            return;
        }

        _editShelter.Latitude = _savedLatitude;
        _editShelter.Longitude = _savedLongitude;
        _editShelter.Address = _savedAddress;
        _editShelter.City = _savedCity;
        _editShelter.Neighborhood = _savedNeighborhood;
        _shelterLocationError = null;
        _suggestedAddressRequestVersion++;
        _suggestedAddress = null;
        _suggestedAddressUnavailable = false;
        Snackbar.Add("Location reverted to the saved shelter details.", Severity.Info);
    }

    private async Task FindShelterCoordinatesAsync()
    {
        if (_editShelter is null)
        {
            return;
        }

        _shelterLocationError = null;
        _shelterFormError = null;
        if (string.IsNullOrWhiteSpace(_editShelter.Address) || string.IsNullOrWhiteSpace(_editShelter.City))
        {
            _shelterLocationError = "Enter city and address before searching for coordinates.";
            return;
        }

        _isGeocoding = true;
        try
        {
            var result = await GeocodingService.FindCoordinatesAsync(_editShelter.Address, _editShelter.City);
            if (result is null)
            {
                _shelterLocationError = "Could not find coordinates for this address. The saved map location was not changed.";
                return;
            }

            _editShelter.Latitude = result.Latitude;
            _editShelter.Longitude = result.Longitude;
            _suggestedAddressRequestVersion++;
            _suggestedAddress = null;
            _suggestedAddressUnavailable = false;
            await LoadSuggestedAddressAsync(result.Latitude, result.Longitude);
            Snackbar.Add("Location found. You can adjust the pin if needed.", Severity.Success);
        }
        catch
        {
            _shelterLocationError = "Could not search for coordinates right now. The saved map location was not changed.";
        }
        finally
        {
            _isGeocoding = false;
        }
    }

    private void OnShelterLocationFieldChanged()
    {
        _shelterLocationError = null;
        _shelterFormError = null;
        _suggestedAddressRequestVersion++;
        _suggestedAddress = null;
        _suggestedAddressUnavailable = false;
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
        if (_editShelter is null || _suggestedAddress is null)
        {
            Snackbar.Add("No suggested address is available for this pin yet.", Severity.Info);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_suggestedAddress.SuggestedAddress))
        {
            _editShelter.Address = _suggestedAddress.SuggestedAddress;
        }

        if (!string.IsNullOrWhiteSpace(_suggestedAddress.City))
        {
            _editShelter.City = _suggestedAddress.City;
        }

        FillNeighborhoodFromSuggestion(_suggestedAddress, overwrite: true);
        _suggestedAddressRequestVersion++;
        _suggestedAddress = null;
        _suggestedAddressUnavailable = false;
        Snackbar.Add("Address updated from selected map location. Save the shelter profile to keep it.", Severity.Success);
    }

    private bool LocationDiffersFromSaved(ShelterEntity shelter)
    {
        return !CoordinatesEqual(shelter.Latitude, _savedLatitude) ||
            !CoordinatesEqual(shelter.Longitude, _savedLongitude) ||
            !string.Equals(NormalizeLocationText(shelter.Address), NormalizeLocationText(_savedAddress), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(NormalizeLocationText(shelter.City), NormalizeLocationText(_savedCity), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(NormalizeLocationText(shelter.Neighborhood), NormalizeLocationText(_savedNeighborhood), StringComparison.OrdinalIgnoreCase);
    }

    private static bool CoordinatesEqual(double? current, double? saved)
    {
        if (!current.HasValue && !saved.HasValue)
        {
            return true;
        }

        if (!current.HasValue || !saved.HasValue)
        {
            return false;
        }

        return Math.Abs(current.Value - saved.Value) < 0.000001;
    }

    private static string NormalizeLocationText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string GetSuggestedAddressPreview(ReverseGeocodingResult result)
    {
        return string.Join(", ", new[] { result.SuggestedAddress, result.Neighborhood, result.City }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct());
    }

    private void FillNeighborhoodFromSuggestion(ReverseGeocodingResult result, bool overwrite = false)
    {
        if (_editShelter is null || string.IsNullOrWhiteSpace(result.Neighborhood))
        {
            return;
        }

        if (overwrite || string.IsNullOrWhiteSpace(_editShelter.Neighborhood))
        {
            _editShelter.Neighborhood = result.Neighborhood.Trim();
        }
    }

    private void ClearShelterEditValidationErrors()
    {
        _shelterFormError = null;
        _shelterEmailError = null;
        _shelterLocationError = null;
        _visitHoursError = null;
    }

    private void ClearShelterEmailError()
    {
        _shelterEmailError = null;
        _shelterFormError = null;
    }

    private void ClearVisitHoursError()
    {
        _visitHoursError = null;
        _shelterFormError = null;
    }

    private bool TryMapShelterEditValidationMessage(string message)
    {
        switch (message)
        {
            case "Shelter email must be a valid email address.":
            case "Another shelter already uses this email address.":
                _shelterEmailError = message;
                return true;
            case "Latitude must be between -90 and 90.":
            case "Longitude must be between -180 and 180.":
                _shelterLocationError = message;
                return true;
            case "Visit start time must be before visit end time.":
                _visitHoursError = message;
                return true;
            default:
                return false;
        }
    }

    private static bool IsCsvFile(IBrowserFile file)
    {
        return Path.GetExtension(file.Name).Equals(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetImportValue(CsvImportRowResult row, string key)
    {
        return row.PreviewData.TryGetValue(key, out var value) ? value : null;
    }

    private static string FormatImportErrors(CsvImportRowResult row)
    {
        return string.Join(Environment.NewLine, row.Errors.Select(error => $"{error.Field}: {error.Message}"));
    }
}

