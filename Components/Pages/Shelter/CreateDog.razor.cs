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

public partial class CreateDog
{
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IDogBreedService DogBreedService { get; set; } = default!;
    [Inject] private IDogImageService DogImageService { get; set; } = default!;
    [Inject] private IFoodTypeService FoodTypeService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly Dog _model = new() { Size = DogSize.Medium, Status = DogStatus.Available };
    private List<FoodType> _foodTypes = [];
    private List<DogBreed> _breedOptions = [];
    private DogBreed? _selectedBreed;
    private DogBreed? _selectedSecondaryBreed;
    private MudForm? _form;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;
    private string? _formError;
    private string? _nameError;
    private string? _breedError;
    private string? _ageYearsError;
    private string? _ageMonthsError;
    private string? _locationError;
    private string? _dailyFoodAmountError;
    private string? _newImageUrl;
    private string? _imageUrlError;
    private int? _shelterId;
    private List<string> _pendingImageUrls = [];
    private bool _useCustomBreed;
    private bool IsMixedBreedDisabled => _useCustomBreed ? false : IsSpecialBreed(_selectedBreed);
    private bool CanUseSecondaryBreed => _model.IsMixedBreed && !_useCustomBreed && _selectedBreed is not null && !IsSpecialBreed(_selectedBreed);
    private bool HasNameError => !string.IsNullOrWhiteSpace(_nameError);
    private bool HasBreedError => !string.IsNullOrWhiteSpace(_breedError);
    private bool HasAgeYearsError => !string.IsNullOrWhiteSpace(_ageYearsError);
    private bool HasAgeMonthsError => !string.IsNullOrWhiteSpace(_ageMonthsError);
    private bool HasLocationError => !string.IsNullOrWhiteSpace(_locationError);
    private bool HasDailyFoodAmountError => !string.IsNullOrWhiteSpace(_dailyFoodAmountError);
    private bool HasImageUrlError => !string.IsNullOrWhiteSpace(_imageUrlError);

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
        }
        catch
        {
            _error = "The dog form could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
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
            if (!AddPendingImageUrlIfPresent())
            {
                return;
            }

            ApplyBreedSelectionToModel();
            await DogService.CreateDogAsync(_model, _shelterId.Value);
            await SavePendingImagesAsync();
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

    private void AddPendingImageUrl()
    {
        AddPendingImageUrlIfPresent(showEmptyMessage: true);
    }

    private bool AddPendingImageUrlIfPresent(bool showEmptyMessage = false)
    {
        _imageUrlError = null;

        if (string.IsNullOrWhiteSpace(_newImageUrl))
        {
            if (showEmptyMessage)
            {
                _imageUrlError = "Enter an image URL first.";
            }

            return true;
        }

        var imageUrl = _newImageUrl.Trim();
        if (!DogImageUrlValidator.TryNormalize(imageUrl, out var normalizedImageUrl))
        {
            _imageUrlError = DogImageUrlValidator.ValidationMessage;
            return false;
        }

        if (_pendingImageUrls.Contains(normalizedImageUrl, StringComparer.OrdinalIgnoreCase))
        {
            _imageUrlError = "This image has already been added for this dog.";
            return false;
        }

        _pendingImageUrls.Add(normalizedImageUrl);
        _newImageUrl = null;
        return true;
    }

    private void RemovePendingImageUrl(string imageUrl)
    {
        _pendingImageUrls.Remove(imageUrl);
        _imageUrlError = null;
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
        _selectedSecondaryBreed = breed;
        _model.SecondaryBreedId = breed?.Id;
        ClearBreedError();
    }

    private void OnMixedBreedChanged(bool isMixedBreed)
    {
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
            case "This image has already been added for this dog.":
            case "Image URL is required.":
            case DogImageUrlValidator.ValidationMessage:
                _imageUrlError = message;
                return true;
            default:
                return false;
        }
    }

    private async Task SavePendingImagesAsync()
    {
        for (var index = 0; index < _pendingImageUrls.Count; index++)
        {
            await DogImageService.AddDogImageAsync(_model.Id, _shelterId!.Value, new DogImage
            {
                ImageUrl = _pendingImageUrls[index],
                IsMainImage = index == 0
            });
        }
    }

    private static bool IsValidImageUrl(string imageUrl)
    {
        return DogImageUrlValidator.IsValidDisplayImageUrl(imageUrl);
    }

    private async Task<int?> GetCurrentShelterIdAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(authState.User);
        if (user is null)
        {
            return null;
        }

        var shelter = await ShelterService.GetShelterForUserAsync(user.Id);
        return shelter?.Id;
    }
}
