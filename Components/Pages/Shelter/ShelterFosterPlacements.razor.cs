using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Shelter;

public partial class ShelterFosterPlacements
{
    [Inject] private IFosterPlacementService FosterPlacementService { get; set; } = default!;
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "dogId")] public int? DogId { get; set; }

    private readonly List<FosterPlacementDto> _placements = [];
    private List<FosterCaregiverProfileDto> _caregivers = [];
    private List<Dog> _dogs = [];
    private FosterPlacementStatsDto? _stats;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;
    private int? _shelterId;
    private string? _currentUserId;

    private int? _selectedDogId;
    private int? _selectedCaregiverId;
    private FosterPlacementPriority _priority = FosterPlacementPriority.Normal;
    private FosterPlacementReason _reason = FosterPlacementReason.Overcrowding;
    private DateTime? _startDate = DateTime.Today;
    private DateTime? _plannedEndDate = DateTime.Today.AddDays(14);
    private string? _careInstructions;
    private string? _medicalNotesSummary;
    private string? _shelterNotes;

    private string? _caregiverName;
    private string? _caregiverEmail;
    private string? _caregiverPhone;
    private string? _caregiverAddress;
    private string? _caregiverExperience;
    private string? _caregiverHome;
    private int _caregiverCapacity = 1;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(authenticationState.User);
            _currentUserId = user?.Id ?? authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                _error = "Current user could not be resolved.";
                return;
            }

            var shelter = await ShelterService.GetShelterForUserAsync(_currentUserId);
            if (shelter is null)
            {
                _error = "No shelter profile is linked to this account.";
                return;
            }

            _shelterId = shelter.Id;
            _dogs = (await DogService.GetDogsForShelterAsync(shelter.Id))
                .Where(dog => dog.Status != DogStatus.Adopted)
                .OrderBy(dog => dog.Name)
                .ToList();
            _selectedDogId ??= DogId is { } dogId && _dogs.Any(dog => dog.Id == dogId)
                ? dogId
                : _dogs.FirstOrDefault()?.Id;

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshAsync()
    {
        if (_shelterId is null)
        {
            return;
        }

        _placements.Clear();
        _placements.AddRange(await FosterPlacementService.GetShelterPlacementsAsync(_shelterId.Value));
        _caregivers = (await FosterPlacementService.GetCaregiversForShelterAsync(_shelterId.Value)).ToList();
        _stats = await FosterPlacementService.GetFosterStatsAsync(_shelterId.Value);

        _selectedCaregiverId ??= _caregivers.FirstOrDefault(caregiver => caregiver.IsActive)?.Id;
    }

    private async Task CreatePlacementAsync()
    {
        if (_shelterId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Shelter account could not be resolved.", Severity.Error);
            return;
        }

        if (_selectedDogId is null)
        {
            Snackbar.Add("Select a dog before creating a foster placement.", Severity.Warning);
            return;
        }

        if (_selectedCaregiverId is null)
        {
            Snackbar.Add("Select a foster caregiver.", Severity.Warning);
            return;
        }

        if (_startDate is null)
        {
            Snackbar.Add("Start date is required.", Severity.Warning);
            return;
        }

        _isSaving = true;
        try
        {
            await FosterPlacementService.CreatePlacementAsync(
                _shelterId.Value,
                _currentUserId,
                new FosterPlacementCreateRequest(
                    _selectedDogId.Value,
                    _selectedCaregiverId.Value,
                    _priority,
                    _reason,
                    _startDate.Value,
                    _plannedEndDate,
                    _careInstructions,
                    _medicalNotesSummary,
                    _shelterNotes));

            Snackbar.Add("Foster placement created.", Severity.Success);
            _careInstructions = null;
            _medicalNotesSummary = null;
            _shelterNotes = null;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task CreateCaregiverAsync()
    {
        if (_shelterId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Shelter account could not be resolved.", Severity.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(_caregiverName) || string.IsNullOrWhiteSpace(_caregiverEmail))
        {
            Snackbar.Add("Caregiver name and email are required.", Severity.Warning);
            return;
        }

        _isSaving = true;
        try
        {
            await FosterPlacementService.CreateCaregiverAsync(
                new FosterCaregiverCreateRequest(
                    _caregiverName,
                    _caregiverEmail,
                    _caregiverPhone,
                    _caregiverAddress,
                    _shelterId,
                    _caregiverExperience,
                    _caregiverHome,
                    _caregiverCapacity,
                    true),
                _currentUserId,
                _shelterId);

            Snackbar.Add("Foster caregiver added.", Severity.Success);
            _caregiverName = null;
            _caregiverEmail = null;
            _caregiverPhone = null;
            _caregiverAddress = null;
            _caregiverExperience = null;
            _caregiverHome = null;
            _caregiverCapacity = 1;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ToggleCaregiverAsync(FosterCaregiverProfileDto caregiver)
    {
        if (_shelterId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            return;
        }

        await RunActionAsync(
            () => FosterPlacementService.UpdateCaregiverAsync(
                caregiver.Id,
                new FosterCaregiverUpdateRequest(
                    caregiver.DisplayName,
                    caregiver.Email,
                    caregiver.PhoneNumber,
                    caregiver.AddressSummary,
                    caregiver.PreferredShelterId,
                    caregiver.ExperienceNotes,
                    caregiver.HomeEnvironmentNotes,
                    caregiver.Capacity,
                    !caregiver.IsActive),
                _currentUserId,
                _shelterId),
            caregiver.IsActive ? "Caregiver deactivated." : "Caregiver activated.");
    }

    private async Task ViewPlacementAsync(int placementId)
    {
        if (_shelterId is null)
        {
            return;
        }

        try
        {
            var placement = await FosterPlacementService.GetPlacementDetailsAsync(placementId, _shelterId.Value);
            var parameters = new DialogParameters<FosterPlacementDetailsDialog>
            {
                { dialog => dialog.Placement, placement }
            };
            var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
            await DialogService.ShowAsync<FosterPlacementDetailsDialog>("Foster placement details", parameters, options);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task ApproveAsync(int placementId)
    {
        var result = await AskForActionAsync("Approve placement", "Approve this pending foster placement?", "Approve", Color.Info);
        if (result is null)
        {
            return;
        }

        await RunActionAsync(
            () => FosterPlacementService.ApprovePlacementAsync(placementId, _shelterId!.Value, _currentUserId!, new FosterPlacementDecisionRequest(result.Notes)),
            "Foster placement approved.");
    }

    private async Task StartAsync(int placementId)
    {
        var result = await AskForActionAsync("Start placement", "Mark this approved placement as active.", "Start", Color.Success);
        if (result is null)
        {
            return;
        }

        await RunActionAsync(
            () => FosterPlacementService.StartPlacementAsync(placementId, _shelterId!.Value, _currentUserId!, new FosterPlacementStartRequest(result.Notes)),
            "Foster placement started.");
    }

    private async Task CompleteAsync(int placementId)
    {
        var result = await AskForActionAsync("Complete placement", "Enter the actual end date and optional completion notes.", "Complete", Color.Primary, requireDate: true, dateLabel: "Actual end date");
        if (result?.Date is null)
        {
            return;
        }

        await RunActionAsync(
            () => FosterPlacementService.CompletePlacementAsync(
                placementId,
                _shelterId,
                _currentUserId!,
                false,
                new FosterPlacementCompleteRequest(result.Date.Value, result.Notes)),
            "Foster placement completed.");
    }

    private async Task CancelAsync(int placementId)
    {
        var result = await AskForActionAsync("Cancel placement", "Add optional notes before cancelling this placement.", "Cancel placement", Color.Default);
        if (result is null)
        {
            return;
        }

        await RunActionAsync(
            () => FosterPlacementService.CancelPlacementAsync(placementId, _shelterId, _currentUserId!, false, new FosterPlacementDecisionRequest(result.Notes)),
            "Foster placement cancelled.");
    }

    private async Task AddNoteAsync(int placementId)
    {
        var result = await AskForActionAsync("Add note", "Add an internal shelter note to this foster placement.", "Save note", Color.Info);
        if (string.IsNullOrWhiteSpace(result?.Notes))
        {
            return;
        }

        await RunActionAsync(
            () => FosterPlacementService.AddPlacementNoteAsync(placementId, _shelterId!.Value, _currentUserId!, new FosterPlacementNoteRequest(result.Notes)),
            "Note added.");
    }

    private async Task<FosterPlacementActionDialog.FosterPlacementActionResult?> AskForActionAsync(
        string title,
        string message,
        string confirmText,
        Color confirmColor,
        bool requireDate = false,
        string dateLabel = "Date")
    {
        var parameters = new DialogParameters<FosterPlacementActionDialog>
        {
            { dialog => dialog.Message, message },
            { dialog => dialog.ConfirmText, confirmText },
            { dialog => dialog.ConfirmColor, confirmColor },
            { dialog => dialog.RequireDate, requireDate },
            { dialog => dialog.DateLabel, dateLabel },
            { dialog => dialog.DefaultDate, DateTime.Today }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<FosterPlacementActionDialog>(title, parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: FosterPlacementActionDialog.FosterPlacementActionResult actionResult }
            ? actionResult
            : null;
    }

    private async Task RunActionAsync(Func<Task> action, string successMessage)
    {
        if (_shelterId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Shelter account could not be resolved.", Severity.Error);
            return;
        }

        _isSaving = true;
        try
        {
            await action();
            Snackbar.Add(successMessage, Severity.Success);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private static string FormatDate(DateTime value) => value.ToLocalTime().ToString("dd MMM yyyy");

    private static string FormatReason(FosterPlacementReason reason) => reason switch
    {
        FosterPlacementReason.MedicalRecovery => "Medical recovery",
        FosterPlacementReason.PuppyCare => "Puppy care",
        FosterPlacementReason.BehavioralObservation => "Behavioral observation",
        FosterPlacementReason.TemporaryEmergencyCare => "Temporary emergency care",
        FosterPlacementReason.AdoptionTrialSupport => "Adoption trial support",
        FosterPlacementReason.Overcrowding => "Overcrowding",
        _ => "Other"
    };

    private static Color GetStatusColor(FosterPlacementStatus status) => status switch
    {
        FosterPlacementStatus.Pending => Color.Warning,
        FosterPlacementStatus.Approved => Color.Info,
        FosterPlacementStatus.Active => Color.Success,
        FosterPlacementStatus.Completed => Color.Primary,
        FosterPlacementStatus.Cancelled => Color.Default,
        _ => Color.Default
    };

    private static Color GetPriorityColor(FosterPlacementPriority priority) => priority switch
    {
        FosterPlacementPriority.Low => Color.Default,
        FosterPlacementPriority.Normal => Color.Info,
        FosterPlacementPriority.High => Color.Warning,
        FosterPlacementPriority.Urgent => Color.Error,
        _ => Color.Default
    };
}
