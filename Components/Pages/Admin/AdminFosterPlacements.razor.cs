using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminFosterPlacements
{
    [Inject] private IFosterPlacementService FosterPlacementService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<FosterPlacementDto> _placements = [];
    private List<Entities.Shelter> _shelters = [];
    private FosterPlacementStatsDto? _stats;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;
    private string? _currentUserId;
    private FosterPlacementStatus? _statusFilter;
    private FosterPlacementPriority? _priorityFilter;
    private FosterPlacementReason? _reasonFilter;
    private int? _shelterFilter;
    private string? _search;

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        try
        {
            var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(authenticationState.User);
            _currentUserId = user?.Id ?? authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier);
            _shelters = await ShelterService.GetAllSheltersAsync();
            await LoadPlacementsAsync();
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

    private async Task LoadPlacementsAsync()
    {
        var filter = new FosterPlacementFilterDto(
            _statusFilter,
            _priorityFilter,
            _reasonFilter,
            _shelterFilter,
            search: _search);

        _placements.Clear();
        _placements.AddRange(await FosterPlacementService.GetAdminPlacementsAsync(filter));
        _stats = await FosterPlacementService.GetFosterStatsAsync();
    }

    private async Task ViewPlacementAsync(int placementId)
    {
        try
        {
            var placement = await FosterPlacementService.GetPlacementDetailsAsync(placementId, isAdmin: true);
            if (placement is null)
            {
                Snackbar.Add("Foster placement was not found.", Severity.Warning);
                return;
            }

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

    private async Task CompleteAsync(int placementId)
    {
        var result = await AskForActionAsync("Complete foster placement", "Enter the actual end date and optional admin completion note.", "Complete", Color.Primary, true);
        if (result?.Date is null)
        {
            return;
        }

        await RunActionAsync(
            () => FosterPlacementService.CompletePlacementAsync(
                placementId,
                null,
                _currentUserId!,
                true,
                new FosterPlacementCompleteRequest(result.Date.Value, result.Notes)),
            "Foster placement completed.");
    }

    private async Task CancelAsync(int placementId)
    {
        var result = await AskForActionAsync("Cancel foster placement", "Add optional admin notes before cancelling this placement.", "Cancel placement", Color.Default);
        if (result is null)
        {
            return;
        }

        await RunActionAsync(
            () => FosterPlacementService.CancelPlacementAsync(placementId, null, _currentUserId!, true, new FosterPlacementDecisionRequest(result.Notes)),
            "Foster placement cancelled.");
    }

    private async Task<FosterPlacementActionDialog.FosterPlacementActionResult?> AskForActionAsync(
        string title,
        string message,
        string confirmText,
        Color confirmColor,
        bool requireDate = false)
    {
        var parameters = new DialogParameters<FosterPlacementActionDialog>
        {
            { dialog => dialog.Message, message },
            { dialog => dialog.ConfirmText, confirmText },
            { dialog => dialog.ConfirmColor, confirmColor },
            { dialog => dialog.RequireDate, requireDate },
            { dialog => dialog.DateLabel, "Actual end date" },
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
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Admin user could not be resolved.", Severity.Error);
            return;
        }

        _isSaving = true;
        try
        {
            await action();
            Snackbar.Add(successMessage, Severity.Success);
            await LoadPlacementsAsync();
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
