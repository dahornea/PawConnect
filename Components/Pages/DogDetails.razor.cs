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

public partial class DogDetails
{
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IFavoriteDogService FavoriteDogService { get; set; } = default!;
    [Inject] private IAdoptionRequestService AdoptionRequestService { get; set; } = default!;
    [Inject] private IAdopterProfileService AdopterProfileService { get; set; } = default!;
    [Inject] private IRecentlyViewedDogService RecentlyViewedDogService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ILogger<DogDetails> Logger { get; set; } = default!;

    [Parameter]
    public int Id { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    private Dog? _dog;
    private bool _isLoading = true;
    private string? _error;
    private string? _currentUserId;
    private bool _currentUserIsAuthenticated;
    private bool _currentUserIsAdopter;
    private bool _currentUserIsShelter;
    private bool _currentUserIsAdmin;
    private bool _isFavorite;
    private bool _hasPendingRequest;
    private bool _showAdoptionForm;
    private bool _isSubmittingRequest;
    private bool _isHistoryLoading;
    private int _selectedImageIndex;
    private readonly HashSet<string> _unavailableImageUrls = new(StringComparer.OrdinalIgnoreCase);
    private AdopterProfile? _adopterProfile;
    private MudForm? _adoptionForm;
    private string? _reasonForAdoption;
    private int? _hoursAlonePerDay;
    private string? _additionalInformation;
    private DateTime? _preferredVisitDate;
    private TimeSpan? _preferredVisitTime;
    private string? _visitSectionValidationError;
    private string? _preferredVisitDateError;
    private string? _preferredVisitTimeError;
    private string? _reasonForAdoptionError;
    private string? _hoursAloneError;

    private bool IsAdopted => _dog?.Status == DogStatus.Adopted;
    private string LoginUrl => $"/Account/Login?returnUrl={Uri.EscapeDataString(NavigationManager.Uri)}";
    private bool HasPreferredVisitDateError => !string.IsNullOrWhiteSpace(_preferredVisitDateError);
    private bool HasPreferredVisitTimeError => !string.IsNullOrWhiteSpace(_preferredVisitTimeError);
    private bool HasReasonForAdoptionError => !string.IsNullOrWhiteSpace(_reasonForAdoptionError);
    private bool HasHoursAloneError => !string.IsNullOrWhiteSpace(_hoursAloneError);
    private string BackHref => LocalReturnUrlHelper.GetSafeLocalPath(ReturnUrl, "/dogs");
    private string BackButtonText => BackHref.Equals("/adopter/copilot", StringComparison.OrdinalIgnoreCase)
        ? "Back to Copilot"
        : BackHref.Equals("/admin/dogs", StringComparison.OrdinalIgnoreCase)
            ? "Back to Admin Dogs"
            : BackHref.Equals("/admin/adoption-requests", StringComparison.OrdinalIgnoreCase)
                ? "Back to Adoption Requests"
                : "Back to Dogs";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadCurrentUserAsync();
            _dog = await DogService.GetDogDetailsAsync(Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Dog details could not be loaded for dog {DogId}.", Id);
            _error = "Dog details could not be loaded. Check the database setup.";
        }
        finally
        {
            _isLoading = false;
        }

        if (_dog is not null && _currentUserIsAdopter && !string.IsNullOrWhiteSpace(_currentUserId))
        {
            await LoadAdopterDogStateAsync();
        }
    }

    private async Task LoadAdopterDogStateAsync()
    {
        if (_dog is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            return;
        }

        try
        {
            await RecentlyViewedDogService.TrackViewAsync(_currentUserId, _dog.Id);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Recently viewed dog tracking failed for adopter {AdopterId} and dog {DogId}.", _currentUserId, _dog.Id);
        }

        try
        {
            _isFavorite = await FavoriteDogService.IsFavoriteAsync(_currentUserId, _dog.Id);
            _hasPendingRequest = await AdoptionRequestService.HasPendingRequestAsync(_currentUserId, _dog.Id);
            _adopterProfile = await AdopterProfileService.GetProfileForUserAsync(_currentUserId);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Optional adopter dog state could not be loaded for adopter {AdopterId} and dog {DogId}.", _currentUserId, _dog.Id);
        }
    }

    private List<DogImage> GetValidImages(Dog dog)
    {
        return DogImageUrlValidator.GetRealDogImages(dog.Images, _unavailableImageUrls);
    }

    private void MarkImageUnavailable(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        _unavailableImageUrls.Add(imageUrl.Trim());
    }

    private int GetSafeSelectedImageIndex(int imageCount)
    {
        if (imageCount <= 0)
        {
            return 0;
        }

        return Math.Clamp(_selectedImageIndex, 0, imageCount - 1);
    }

    private void SelectGalleryImage(int imageIndex)
    {
        _selectedImageIndex = imageIndex;
    }

    private async Task OpenImagePreviewAsync(IReadOnlyList<DogImage> images, int initialIndex)
    {
        if (_dog is null || images.Count == 0)
        {
            return;
        }

        var parameters = new DialogParameters
        {
            ["DogName"] = _dog.Name,
            ["FormattedBreed"] = DogBreedFormatter.Format(_dog),
            ["Images"] = images,
            ["InitialIndex"] = initialIndex
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.False,
            NoHeader = true,
            CloseOnEscapeKey = true,
            BackgroundClass = "dog-image-lightbox-backdrop"
        };

        await DialogService.ShowAsync<DogImagePreviewDialog>(_dog.Name, parameters, options);
    }

    private static string GetDogImageAlt(Dog dog)
    {
        return $"Photo of {dog.Name}, {DogBreedFormatter.Format(dog)}";
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

    private static Color GetCompatibilityColor(CatCompatibility value) => value switch
    {
        CatCompatibility.Yes => Color.Success,
        CatCompatibility.SlowIntroductions => Color.Warning,
        CatCompatibility.No => Color.Error,
        _ => Color.Default
    };

    private static Color GetCompatibilityColor(DogCompatibility value) => value switch
    {
        DogCompatibility.Yes => Color.Success,
        DogCompatibility.CalmDogsOnly or DogCompatibility.SlowIntroductions => Color.Warning,
        DogCompatibility.OnlyDog or DogCompatibility.No => Color.Error,
        _ => Color.Default
    };

    private static Color GetCompatibilityColor(ChildrenCompatibility value) => value switch
    {
        ChildrenCompatibility.Yes => Color.Success,
        ChildrenCompatibility.OlderChildrenOnly => Color.Warning,
        ChildrenCompatibility.No => Color.Error,
        _ => Color.Default
    };

    private static Color GetCompatibilityColor(ApartmentSuitability value) => value switch
    {
        ApartmentSuitability.Suitable => Color.Success,
        ApartmentSuitability.MaybeWithRoutine => Color.Warning,
        ApartmentSuitability.NotRecommended => Color.Error,
        _ => Color.Default
    };

    private static Color GetActivityColor(DogActivityLevel value) => value switch
    {
        DogActivityLevel.Low => Color.Success,
        DogActivityLevel.Medium => Color.Info,
        DogActivityLevel.High => Color.Warning,
        _ => Color.Default
    };

    private static Color GetExperienceColor(DogExperienceNeeded value) => value switch
    {
        DogExperienceNeeded.Beginner => Color.Success,
        DogExperienceNeeded.SomeExperience => Color.Info,
        DogExperienceNeeded.Experienced => Color.Warning,
        _ => Color.Default
    };

    private async Task LoadCurrentUserAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var principal = authState.User;

        _currentUserIsAuthenticated = principal.Identity?.IsAuthenticated == true;
        _currentUserIsAdopter = _currentUserIsAuthenticated && principal.IsInRole("Adopter");
        _currentUserIsShelter = _currentUserIsAuthenticated && principal.IsInRole("Shelter");
        _currentUserIsAdmin = _currentUserIsAuthenticated && principal.IsInRole("Admin");
        if (!_currentUserIsAdopter)
        {
            _currentUserId = null;
            return;
        }

        var user = await UserManager.GetUserAsync(principal);
        _currentUserId = user?.Id;
    }

    private async Task ToggleFavoriteAsync()
    {
        if (_dog is null)
        {
            return;
        }

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
            if (_isFavorite)
            {
                await FavoriteDogService.RemoveFavoriteAsync(_currentUserId, _dog.Id);
                _isFavorite = false;
                Snackbar.Add("Dog removed from favorites.", Severity.Success);
            }
            else
            {
                await FavoriteDogService.AddFavoriteAsync(_currentUserId, _dog.Id);
                _isFavorite = true;
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

    private async Task StartAdoptionRequestAsync()
    {
        if (_dog is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Please log in with an adopter account to submit an adoption request.", Severity.Info);
            NavigationManager.NavigateTo($"/Account/Login?returnUrl={Uri.EscapeDataString(NavigationManager.Uri)}");
            return;
        }

        if (!_currentUserIsAdopter)
        {
            Snackbar.Add("Only adopter accounts can submit adoption requests.", Severity.Warning);
            return;
        }

        if (_dog.Status is DogStatus.Adopted or DogStatus.InTreatment)
        {
            Snackbar.Add("Adoption requests can only be submitted for available or reserved dogs.", Severity.Warning);
            return;
        }

        _hasPendingRequest = await AdoptionRequestService.HasPendingRequestAsync(_currentUserId, _dog.Id);
        if (_hasPendingRequest)
        {
            Snackbar.Add("You already have a pending request for this dog.", Severity.Info);
            return;
        }

        _showAdoptionForm = true;
        ClearAdoptionValidationErrors();
        _preferredVisitDate ??= DateTime.Today.AddDays(1);
        _preferredVisitTime ??= VisitSchedulingHelper.GetVisitStartTime(_dog.Shelter);
    }

    private async Task OpenStatusHistoryDialogAsync()
    {
        if (_dog is null)
        {
            return;
        }

        _isHistoryLoading = true;

        try
        {
            var statusHistory = await DogService.GetStatusHistoryForDogAsync(_dog.Id);
            var parameters = new DialogParameters
            {
                ["DogName"] = _dog.Name,
                ["Histories"] = statusHistory
            };

            await DialogService.ShowAsync<DogStatusHistoryDialog>("Status History", parameters);
        }
        catch
        {
            Snackbar.Add("Status history could not be loaded right now.", Severity.Error);
        }
        finally
        {
            _isHistoryLoading = false;
        }
    }

    private async Task SubmitAdoptionRequestAsync()
    {
        if (_dog is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Current adopter account could not be found.", Severity.Error);
            return;
        }

        _isSubmittingRequest = true;

        try
        {
            if (_adoptionForm is not null)
            {
                await _adoptionForm.ValidateAsync();
                if (!_adoptionForm.IsValid)
                {
                    return;
                }
            }

            var questionnaire = new AdoptionRequestQuestionnaire(
                _reasonForAdoption ?? string.Empty,
                _hoursAlonePerDay,
                _additionalInformation,
                GetPreferredVisitDateTime());

            ClearAdoptionValidationErrors();
            await AdoptionRequestService.CreateRequestAsync(_currentUserId, _dog.Id, questionnaire);
            _hasPendingRequest = true;
            _showAdoptionForm = false;
            await ResetAdoptionFormAsync();
            Snackbar.Add("Adoption request submitted successfully.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            if (!TryMapAdoptionValidationMessage(ex.Message))
            {
                Snackbar.Add(ex.Message, Severity.Warning);
            }
        }
        catch
        {
            Snackbar.Add("Could not submit the request. Please try again.", Severity.Error);
        }
        finally
        {
            _isSubmittingRequest = false;
        }
    }

    private async Task CancelAdoptionForm()
    {
        _showAdoptionForm = false;
        await ResetAdoptionFormAsync();
    }

    private async Task ResetAdoptionFormAsync()
    {
        _reasonForAdoption = null;
        _hoursAlonePerDay = null;
        _additionalInformation = null;
        _preferredVisitDate = null;
        _preferredVisitTime = null;
        ClearAdoptionValidationErrors();
        if (_adoptionForm is not null)
        {
            await _adoptionForm.ResetValidationAsync();
        }
    }

    private string GetFavoriteIcon()
    {
        return _isFavorite ? Icons.Material.Filled.Favorite : Icons.Material.Outlined.FavoriteBorder;
    }

    private Color GetFavoriteColor()
    {
        return _isFavorite ? Color.Secondary : Color.Default;
    }

    private Variant GetFavoriteVariant()
    {
        return _isFavorite ? Variant.Filled : Variant.Outlined;
    }

    private string GetFavoriteButtonText()
    {
        return _isFavorite ? "Remove from Favorites" : "Add to Favorites";
    }

    private string GetAdoptionButtonText()
    {
        return _hasPendingRequest ? "Request Pending" : "Submit Adoption Request";
    }

    private DateTime? GetPreferredVisitDateTime()
    {
        if (!_preferredVisitDate.HasValue || !_preferredVisitTime.HasValue)
        {
            return null;
        }

        return _preferredVisitDate.Value.Date.Add(_preferredVisitTime.Value);
    }

    private void ClearAdoptionValidationErrors()
    {
        ClearVisitValidationErrors();
        ClearQuestionnaireValidationErrors();
        ClearHoursAloneValidationError();
    }

    private void ClearVisitValidationErrors()
    {
        _visitSectionValidationError = null;
        _preferredVisitDateError = null;
        _preferredVisitTimeError = null;
    }

    private void ClearQuestionnaireValidationErrors()
    {
        _reasonForAdoptionError = null;
    }

    private void ClearHoursAloneValidationError()
    {
        _hoursAloneError = null;
    }

    private bool TryMapAdoptionValidationMessage(string message)
    {
        switch (message)
        {
            case "Preferred visit time is required.":
                _preferredVisitDateError = _preferredVisitDate.HasValue ? null : "Preferred visit date is required.";
                _preferredVisitTimeError = _preferredVisitTime.HasValue ? null : message;
                return true;
            case "Please choose a future visit time.":
                if (_preferredVisitDate.HasValue && _preferredVisitDate.Value.Date < DateTime.Today)
                {
                    _preferredVisitDateError = message;
                }
                else
                {
                    _preferredVisitTimeError = message;
                }

                return true;
            case "This shelter is closed for visits on the selected day.":
                _preferredVisitDateError = message;
                return true;
            case "Please choose a time within the shelter's visiting hours.":
                _preferredVisitTimeError = message;
                return true;
            case "Shelter visiting hours are not configured correctly.":
                _visitSectionValidationError = message;
                return true;
            case "Reason for adoption is required.":
                _reasonForAdoptionError = message;
                return true;
            case "Hours alone per day must be between 0 and 24.":
                _hoursAloneError = message;
                return true;
            default:
                return false;
        }
    }

    private string GetShelterVisitingHours()
    {
        return VisitSchedulingHelper.FormatVisitingHours(_dog?.Shelter);
    }

    private static string FormatYesNo(bool value)
    {
        return value ? "Yes" : "No";
    }
}
