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

public partial class AdminTransfers
{
    [Inject] private IDogTransferService DogTransferService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<DogTransferRequestDto> _transfers = [];
    private List<Entities.Shelter> _shelters = [];
    private DogTransferStatsDto? _stats;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;
    private string? _currentUserId;
    private DogTransferStatus? _statusFilter;
    private DogTransferPriority? _priorityFilter;
    private int? _sourceShelterFilter;
    private int? _destinationShelterFilter;
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
            await LoadTransfersAsync();
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

    private async Task LoadTransfersAsync()
    {
        var filter = new DogTransferFilterDto(
            _statusFilter,
            _priorityFilter,
            _sourceShelterFilter,
            _destinationShelterFilter,
            _search);

        var transfers = await DogTransferService.GetAdminTransfersAsync(filter);
        _transfers.Clear();
        _transfers.AddRange(transfers);
        _stats = await DogTransferService.GetTransferStatsAsync();
    }

    private async Task ViewTransferAsync(int transferId)
    {
        try
        {
            var transfer = await DogTransferService.GetTransferDetailsAsync(transferId, isAdmin: true);
            if (transfer is null)
            {
                Snackbar.Add("Transfer request was not found.", Severity.Warning);
                return;
            }

            var parameters = new DialogParameters<DogTransferDetailsDialog>
            {
                { dialog => dialog.Transfer, transfer },
                { dialog => dialog.ShowAdminNotes, true }
            };
            var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
            await DialogService.ShowAsync<DogTransferDetailsDialog>("Transfer details", parameters, options);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task UpdateAdminNoteAsync(int transferId)
    {
        var notes = await AskForNotesAsync(
            "Admin note",
            "Add or replace the admin note for this transfer request.",
            "Save note",
            Color.Info);
        if (notes is null)
        {
            return;
        }

        await RunTransferActionAsync(
            () => DogTransferService.UpdateAdminNotesAsync(transferId, notes, _currentUserId!),
            "Admin note saved.");
    }

    private async Task CancelAsync(int transferId)
    {
        var notes = await AskForNotesAsync(
            "Cancel transfer",
            "Add optional admin notes before cancelling this pending transfer.",
            "Cancel transfer",
            Color.Default);
        if (notes is null)
        {
            return;
        }

        await RunTransferActionAsync(
            () => DogTransferService.CancelTransferAsync(transferId, null, _currentUserId!, true, new DogTransferDecisionRequest(notes)),
            "Transfer request cancelled.");
    }

    private async Task CompleteAsync(int transferId)
    {
        var notes = await AskForNotesAsync(
            "Complete transfer",
            "Add optional admin notes before completing this approved transfer.",
            "Complete transfer",
            Color.Primary);
        if (notes is null)
        {
            return;
        }

        await RunTransferActionAsync(
            () => DogTransferService.CompleteTransferAsync(transferId, null, _currentUserId!, true, new DogTransferCompleteRequest(notes)),
            "Transfer completed and dog shelter updated.");
    }

    private async Task<string?> AskForNotesAsync(string title, string message, string confirmText, Color confirmColor)
    {
        var parameters = new DialogParameters<DogTransferDecisionDialog>
        {
            { dialog => dialog.Message, message },
            { dialog => dialog.ConfirmText, confirmText },
            { dialog => dialog.ConfirmColor, confirmColor }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<DogTransferDecisionDialog>(title, parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: string notes } ? notes : null;
    }

    private async Task RunTransferActionAsync(Func<Task> action, string successMessage)
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
            await LoadTransfersAsync();
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

    private static string FormatStatus(DogTransferStatus status) => status switch
    {
        DogTransferStatus.Pending => "Pending",
        DogTransferStatus.Approved => "Approved",
        DogTransferStatus.Rejected => "Rejected",
        DogTransferStatus.Cancelled => "Cancelled",
        DogTransferStatus.Completed => "Completed",
        _ => status.ToString()
    };

    private static Color GetStatusColor(DogTransferStatus status) => status switch
    {
        DogTransferStatus.Pending => Color.Warning,
        DogTransferStatus.Approved => Color.Info,
        DogTransferStatus.Rejected => Color.Error,
        DogTransferStatus.Cancelled => Color.Default,
        DogTransferStatus.Completed => Color.Success,
        _ => Color.Default
    };

    private static Color GetPriorityColor(DogTransferPriority priority) => priority switch
    {
        DogTransferPriority.Low => Color.Default,
        DogTransferPriority.Normal => Color.Info,
        DogTransferPriority.High => Color.Warning,
        DogTransferPriority.Urgent => Color.Error,
        _ => Color.Default
    };

    private static string FormatDateTime(DateTime value) =>
        value.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
}
