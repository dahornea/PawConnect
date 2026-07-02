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

namespace PawConnect.Components.Pages.Shelter;

public partial class EditDog
{
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IDogBreedService DogBreedService { get; set; } = default!;
    [Inject] private IFoodTypeService FoodTypeService { get; set; } = default!;
    [Inject] private IDogImageService DogImageService { get; set; } = default!;
    [Inject] private IMedicalRecordService MedicalRecordService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    [Parameter]
    public int Id { get; set; }

    private Dog? _model;
    private List<FoodType> _foodTypes = [];
    private List<DogBreed> _breedOptions = [];
    private DogBreed? _selectedBreed;
    private DogBreed? _selectedSecondaryBreed;
    private List<DogImage> _images = [];
    private readonly HashSet<string> _unavailableImageUrls = new(StringComparer.OrdinalIgnoreCase);
    private List<MedicalRecord> _medicalRecords = [];
    private List<DogStatusHistory> _statusHistory = [];
    private DogImage _newImage = new();
    private MedicalRecord _medicalRecordModel = new() { RecordDate = DateTime.Today };
    private DateTime? _medicalRecordDate = DateTime.Today;
    private MudForm? _form;
    private MudForm? _imageForm;
    private MudForm? _medicalForm;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isImageSaving;
    private bool _isMedicalSaving;
    private bool _isSuccessStorySaving;
    private string? _error;
    private string? _formError;
    private string? _nameError;
    private string? _breedError;
    private string? _ageYearsError;
    private string? _ageMonthsError;
    private string? _locationError;
    private string? _dailyFoodAmountError;
    private string? _imageUrlError;
    private string? _statusHistoryError;
    private string? _successStoryError;
    private string? _successStoryText;
    private DateTime? _adoptedAt;
    private int? _shelterId;
    private string? _currentUserId;
    private bool _useCustomBreed;

    private bool IsReadOnly => _model?.Status == DogStatus.Adopted;
    private bool IsMixedBreedDisabled => _useCustomBreed ? false : IsSpecialBreed(_selectedBreed);
    private bool CanUseSecondaryBreed => _model?.IsMixedBreed == true && !_useCustomBreed && _selectedBreed is not null && !IsSpecialBreed(_selectedBreed);
    private bool HasNameError => !string.IsNullOrWhiteSpace(_nameError);
    private bool HasBreedError => !string.IsNullOrWhiteSpace(_breedError);
    private bool HasAgeYearsError => !string.IsNullOrWhiteSpace(_ageYearsError);
    private bool HasAgeMonthsError => !string.IsNullOrWhiteSpace(_ageMonthsError);
    private bool HasLocationError => !string.IsNullOrWhiteSpace(_locationError);
    private bool HasDailyFoodAmountError => !string.IsNullOrWhiteSpace(_dailyFoodAmountError);
    private bool HasImageUrlError => !string.IsNullOrWhiteSpace(_imageUrlError);
    private bool HasSuccessStoryError => !string.IsNullOrWhiteSpace(_successStoryError);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _shelterId = await GetCurrentShelterIdAsync();
            if (_shelterId is null)
            {
                _error = "No shelter profile is linked to this account.";
                return;
            }

            _foodTypes = await FoodTypeService.GetAllAsync();
            _breedOptions = await DogBreedService.GetActiveBreedsAsync();
            _model = await DogService.GetDogForShelterAsync(Id, _shelterId.Value);
            if (_model is null)
            {
                _error = "Dog was not found for your shelter.";
                return;
            }

            if (_model.AgeYears == 0 && _model.AgeMonths == 0 && _model.Age > 0)
            {
                _model.AgeYears = _model.Age;
            }

            InitializeBreedSelection();
            _successStoryText = _model.SuccessStoryText;
            _adoptedAt = _model.AdoptedAt;
            await LoadDogManagementDataAsync();
        }
        catch
        {
            _error = "The dog could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_model is null || _shelterId is null)
        {
            Snackbar.Add("Current dog or shelter profile could not be found.", Severity.Error);
            return;
        }

        if (IsReadOnly)
        {
            Snackbar.Add("Adopted dogs are read-only for shelter users.", Severity.Info);
            return;
        }

        if (_form is not null)
        {
            await _form.ValidateAsync();
            if (!_form.IsValid)
            {
                return;
            }
        }

        ClearDogValidationErrors();
        _isSaving = true;

        try
        {
            ApplyBreedSelectionToModel();
            await DogService.UpdateDogAsync(_model, _shelterId.Value, _currentUserId);
            Snackbar.Add("Dog profile saved.", Severity.Success);
            NavigationManager.NavigateTo("/shelter/dogs");
        }
        catch (InvalidOperationException ex)
        {
            if (!TryMapDogValidationMessage(ex.Message))
            {
                _formError = ex.Message;
            }
        }
        catch
        {
            Snackbar.Add("Could not save dog profile. Please try again.", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task SaveSuccessStoryAsync()
    {
        if (_model is null || _shelterId is null)
        {
            Snackbar.Add("Current dog or shelter profile could not be found.", Severity.Error);
            return;
        }

        _isSuccessStorySaving = true;
        _successStoryError = null;

        try
        {
            await DogService.UpdateSuccessStoryAsync(_model.Id, _shelterId.Value, _successStoryText, _adoptedAt);
            _model.SuccessStoryText = string.IsNullOrWhiteSpace(_successStoryText) ? null : _successStoryText.Trim();
            _model.AdoptedAt = _adoptedAt;
            Snackbar.Add("Adoption success story saved.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message == "Success story text must be 2000 characters or fewer.")
            {
                _successStoryError = ex.Message;
            }
            else
            {
                Snackbar.Add(ex.Message, Severity.Warning);
            }
        }
        catch
        {
            Snackbar.Add("Could not save success story. Please try again.", Severity.Error);
        }
        finally
        {
            _isSuccessStorySaving = false;
        }
    }

    private async Task<int?> GetCurrentShelterIdAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(authState.User);
        if (user is null)
        {
            return null;
        }

        _currentUserId = user.Id;
        var shelter = await ShelterService.GetShelterForUserAsync(user.Id);
        return shelter?.Id;
    }

    private async Task LoadDogManagementDataAsync()
    {
        _images = await DogImageService.GetImagesForDogAsync(Id);
        _medicalRecords = await MedicalRecordService.GetMedicalRecordsForDogAsync(Id);
        if (_shelterId is not null)
        {
            try
            {
                _statusHistory = await DogService.GetStatusHistoryForShelterDogAsync(Id, _shelterId.Value);
                _statusHistoryError = null;
            }
            catch
            {
                _statusHistory = [];
                _statusHistoryError = "Status history could not be loaded. If this is a new feature, apply the latest database migration.";
            }
        }
    }

    private async Task AddImageAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        _imageUrlError = null;
        if (string.IsNullOrWhiteSpace(_newImage.ImageUrl))
        {
            _imageUrlError = "Image URL is required.";
            return;
        }

        if (!DogImageUrlValidator.TryNormalize(_newImage.ImageUrl, out var normalizedImageUrl))
        {
            _imageUrlError = DogImageUrlValidator.ValidationMessage;
            return;
        }

        _isImageSaving = true;

        try
        {
            if (_images.Count == 0)
            {
                _newImage.IsMainImage = true;
            }

            _newImage.ImageUrl = normalizedImageUrl;
            await DogImageService.AddDogImageAsync(Id, _shelterId.Value, _newImage);
            _newImage = new DogImage();
            if (_imageForm is not null)
            {
                await _imageForm.ResetValidationAsync();
            }
            _imageUrlError = null;
            await LoadDogManagementDataAsync();
            Snackbar.Add("Dog image added.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            if (!TryMapImageValidationMessage(ex.Message))
            {
                Snackbar.Add(ex.Message, Severity.Warning);
            }
        }
        catch
        {
            Snackbar.Add("Could not add dog image. Please try again.", Severity.Error);
        }
        finally
        {
            _isImageSaving = false;
        }
    }

    private async Task SetMainImageAsync(DogImage image)
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        try
        {
            await DogImageService.SetMainImageAsync(image.Id, _shelterId.Value);
            foreach (var dogImage in _images)
            {
                dogImage.IsMainImage = dogImage.Id == image.Id;
            }

            Snackbar.Add("Main image updated.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            if (!TryMapImageValidationMessage(ex.Message))
            {
                Snackbar.Add(ex.Message, Severity.Warning);
            }
        }
        catch
        {
            Snackbar.Add("Could not update main image. Please try again.", Severity.Error);
        }
    }

    private async Task DeleteImageAsync(DogImage image)
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        if (!await ConfirmAsync(
            "Delete image",
            "Are you sure you want to delete this image? This action cannot easily be undone.",
            "Delete",
            Color.Error,
            Icons.Material.Filled.DeleteForever))
        {
            return;
        }

        try
        {
            await DogImageService.DeleteDogImageAsync(image.Id, _shelterId.Value);
            _images.Remove(image);
            Snackbar.Add("Dog image deleted.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            if (!TryMapImageValidationMessage(ex.Message))
            {
                Snackbar.Add(ex.Message, Severity.Warning);
            }
        }
        catch
        {
            Snackbar.Add("Could not delete dog image. Please try again.", Severity.Error);
        }
    }

    private async Task SaveMedicalRecordAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        if (_medicalForm is not null)
        {
            await _medicalForm.ValidateAsync();
            if (!_medicalForm.IsValid || !_medicalRecordDate.HasValue)
            {
                return;
            }
        }

        _isMedicalSaving = true;
        _medicalRecordModel.RecordDate = _medicalRecordDate!.Value;

        try
        {
            if (_medicalRecordModel.Id == 0)
            {
                await MedicalRecordService.AddMedicalRecordAsync(Id, _shelterId.Value, _medicalRecordModel);
                Snackbar.Add("Medical record saved.", Severity.Success);
            }
            else
            {
                await MedicalRecordService.UpdateMedicalRecordAsync(_shelterId.Value, _medicalRecordModel);
                Snackbar.Add("Medical record saved.", Severity.Success);
            }

            await ResetMedicalFormAsync();
            await LoadDogManagementDataAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not save medical record. Please try again.", Severity.Error);
        }
        finally
        {
            _isMedicalSaving = false;
        }
    }

    private void EditMedicalRecord(MedicalRecord record)
    {
        _medicalRecordModel = new MedicalRecord
        {
            Id = record.Id,
            DogId = record.DogId,
            VaccineName = record.VaccineName,
            TreatmentDescription = record.TreatmentDescription,
            RecordDate = record.RecordDate,
            Notes = record.Notes
        };
        _medicalRecordDate = record.RecordDate;
    }

    private async Task DeleteMedicalRecordAsync(MedicalRecord record)
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        if (!await ConfirmAsync(
            "Delete medical record",
            "Are you sure you want to delete this medical record? This action cannot easily be undone.",
            "Delete",
            Color.Error,
            Icons.Material.Filled.DeleteForever))
        {
            return;
        }

        try
        {
            await MedicalRecordService.DeleteMedicalRecordAsync(record.Id, _shelterId.Value);
            _medicalRecords.Remove(record);
            Snackbar.Add("Medical record deleted.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not delete medical record. Please try again.", Severity.Error);
        }
    }

    private async Task ResetMedicalFormAsync()
    {
        _medicalRecordModel = new MedicalRecord { RecordDate = DateTime.Today };
        _medicalRecordDate = DateTime.Today;
        if (_medicalForm is not null)
        {
            await _medicalForm.ResetValidationAsync();
        }
    }

    private void ClearDogValidationErrors()
    {
        _formError = null;
        _nameError = null;
        _breedError = null;
        ClearAgeErrors();
        _locationError = null;
        _dailyFoodAmountError = null;
    }

    private void ClearDogNameError()
    {
        _nameError = null;
        _formError = null;
    }

    private void ClearBreedError()
    {
        _breedError = null;
        _formError = null;
    }

    private void InitializeBreedSelection()
    {
        if (_model is null)
        {
            return;
        }

        _selectedBreed = _model.DogBreedId.HasValue
            ? _breedOptions.FirstOrDefault(breed => breed.Id == _model.DogBreedId.Value)
            : null;
        _selectedSecondaryBreed = _model.SecondaryBreedId.HasValue
            ? _breedOptions.FirstOrDefault(breed => breed.Id == _model.SecondaryBreedId.Value)
            : null;

        if (_selectedBreed is null && !string.IsNullOrWhiteSpace(_model.Breed))
        {
            var parsed = DogBreedFormatter.Parse(_model.Breed, _breedOptions);
            _model.DogBreedId = parsed.DogBreedId;
            _model.SecondaryBreedId = parsed.SecondaryBreedId;
            _model.IsMixedBreed = parsed.IsMixedBreed;
            _model.CustomBreedName = parsed.CustomBreedName;
            _model.Breed = parsed.DisplayName;
            _selectedBreed = parsed.DogBreedId.HasValue
                ? _breedOptions.FirstOrDefault(breed => breed.Id == parsed.DogBreedId.Value)
                : null;
            _selectedSecondaryBreed = parsed.SecondaryBreedId.HasValue
                ? _breedOptions.FirstOrDefault(breed => breed.Id == parsed.SecondaryBreedId.Value)
                : null;
        }

        _useCustomBreed = _selectedBreed is null && !string.IsNullOrWhiteSpace(_model.CustomBreedName);
    }

    private Task<IEnumerable<DogBreed>> SearchBreedOptionsAsync(string? value, CancellationToken cancellationToken)
    {
        IEnumerable<DogBreed> results = _breedOptions;
        if (!string.IsNullOrWhiteSpace(value))
        {
            results = results.Where(breed => breed.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(results.Take(20));
    }

    private Task<IEnumerable<DogBreed>> SearchSecondaryBreedOptionsAsync(string? value, CancellationToken cancellationToken)
    {
        IEnumerable<DogBreed> results = _breedOptions
            .Where(breed => !IsSpecialBreed(breed) && breed.Id != _selectedBreed?.Id);
        if (!string.IsNullOrWhiteSpace(value))
        {
            results = results.Where(breed => breed.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(results.Take(20));
    }

    private static string FormatBreedOption(DogBreed? breed)
    {
        return breed?.Name ?? string.Empty;
    }

    private void OnBreedSelected(DogBreed? breed)
    {
        if (_model is null)
        {
            return;
        }

        _selectedBreed = breed;
        _model.DogBreedId = breed?.Id;
        if (IsSpecialBreed(breed))
        {
            _model.IsMixedBreed = false;
            _selectedSecondaryBreed = null;
            _model.SecondaryBreedId = null;
        }
        else if (_selectedSecondaryBreed?.Id == breed?.Id)
        {
            _selectedSecondaryBreed = null;
            _model.SecondaryBreedId = null;
        }

        ClearBreedError();
    }

    private void OnSecondaryBreedSelected(DogBreed? breed)
    {
        if (_model is null)
        {
            return;
        }

        _selectedSecondaryBreed = breed;
        _model.SecondaryBreedId = breed?.Id;
        ClearBreedError();
    }

    private void OnMixedBreedChanged(bool isMixedBreed)
    {
        if (_model is null)
        {
            return;
        }

        _model.IsMixedBreed = isMixedBreed;
        if (!isMixedBreed)
        {
            _selectedSecondaryBreed = null;
            _model.SecondaryBreedId = null;
        }

        ClearBreedError();
    }

    private void OnUseCustomBreedChanged(bool useCustomBreed)
    {
        if (_model is null)
        {
            return;
        }

        _useCustomBreed = useCustomBreed;
        if (useCustomBreed)
        {
            _selectedBreed = null;
            _selectedSecondaryBreed = null;
            _model.DogBreedId = null;
            _model.SecondaryBreedId = null;
        }
        else
        {
            _model.CustomBreedName = null;
        }

        ClearBreedError();
    }

    private void ApplyBreedSelectionToModel()
    {
        if (_model is null)
        {
            return;
        }

        if (_useCustomBreed)
        {
            _model.DogBreedId = null;
            _model.SecondaryBreedId = null;
            _model.Breed = DogBreedFormatter.Format(null, _model.IsMixedBreed, _model.CustomBreedName, _model.Breed);
            return;
        }

        _model.DogBreedId = _selectedBreed?.Id;
        _model.SecondaryBreedId = CanUseSecondaryBreed ? _selectedSecondaryBreed?.Id : null;
        _model.CustomBreedName = null;
        if (IsSpecialBreed(_selectedBreed))
        {
            _model.IsMixedBreed = false;
            _model.SecondaryBreedId = null;
        }

        _model.Breed = DogBreedFormatter.Format(_selectedBreed?.Name, _selectedSecondaryBreed?.Name, _model.IsMixedBreed, null, _model.Breed);
    }

    private static bool IsSpecialBreed(DogBreed? breed)
    {
        return breed is not null &&
            (breed.Name.Equals("Mixed Breed", StringComparison.OrdinalIgnoreCase) ||
             breed.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase));
    }

    private void ClearAgeErrors()
    {
        _ageYearsError = null;
        _ageMonthsError = null;
        _formError = null;
    }

    private void ClearLocationError()
    {
        _locationError = null;
        _formError = null;
    }

    private void ClearDailyFoodAmountError()
    {
        _dailyFoodAmountError = null;
        _formError = null;
    }

    private void ClearImageUrlError()
    {
        _imageUrlError = null;
    }

    private void ClearSuccessStoryError()
    {
        _successStoryError = null;
    }

    private bool TryMapDogValidationMessage(string message)
    {
        switch (message)
        {
            case "Dog name is required.":
                _nameError = message;
                return true;
            case "Breed is required.":
                _breedError = message;
                return true;
            case "Age in years must be between 0 and 30.":
                _ageYearsError = message;
                return true;
            case "Age in months must be between 0 and 11.":
                _ageMonthsError = message;
                return true;
            case "Please enter the dog's age in years or months.":
                _ageYearsError = message;
                _ageMonthsError = message;
                return true;
            case "Location is required.":
                _locationError = message;
                return true;
            case "Daily food amount must be zero or greater when provided.":
                _dailyFoodAmountError = message;
                return true;
            default:
                return false;
        }
    }

    private bool TryMapImageValidationMessage(string message)
    {
        switch (message)
        {
            case "This image has already been added for this dog.":
            case "Image URL is required.":
            case DogImageUrlValidator.ValidationMessage:
                _imageUrlError = message;
                return true;
            default:
                return false;
        }
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

    private static bool IsValidImageUrl(string imageUrl)
    {
        return DogImageUrlValidator.IsValidDisplayImageUrl(imageUrl);
    }

    private bool IsRealDisplayImage(DogImage image)
    {
        return DogImageUrlValidator.IsValidRealDogImageUrl(image.ImageUrl) &&
            !IsImageUnavailable(image.ImageUrl);
    }

    private bool IsImageUnavailable(string? imageUrl)
    {
        return !string.IsNullOrWhiteSpace(imageUrl) &&
            _unavailableImageUrls.Contains(imageUrl.Trim());
    }

    private void MarkImageUnavailable(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        _unavailableImageUrls.Add(imageUrl.Trim());
    }

    private async Task<bool> ConfirmAsync(string title, string message, string confirmText, Color confirmColor, string icon)
    {
        var parameters = new DialogParameters
        {
            ["Title"] = title,
            ["Message"] = message,
            ["ConfirmText"] = confirmText,
            ["ConfirmColor"] = confirmColor,
            ["IconColor"] = confirmColor,
            ["Icon"] = icon
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>(title, parameters);
        var result = await dialog.Result;
        return result is not null && !result.Canceled;
    }
}
