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

public partial class ShelterTransfers
{
    [Inject] private IDogTransferService DogTransferService { get; set; } = default!;
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "dogId")] public int? DogId { get; set; }

    private readonly List<DogTransferRequestDto> _incoming = [];
    private readonly List<DogTransferRequestDto> _outgoing = [];
    private readonly List<DogTransferRequestDto> _history = [];
    private List<Dog> _dogs = [];
    private List<Entities.Shelter> _shelters = [];
    private DogTransferStatsDto? _stats;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;
    private int? _shelterId;
    private string? _currentUserId;
    private int? _selectedDogId;
    private int? _destinationShelterId;
    private DogTransferPriority _priority = DogTransferPriority.Normal;
    private string? _reason;
    private string? _sourceNotes;

    private IEnumerable<Entities.Shelter> DestinationShelterOptions =>
        _shelters.Where(shelter => shelter.Id != _shelterId).OrderBy(shelter => shelter.Name);

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
                .OrderBy(dog => dog.Name)
                .ToList();
            _shelters = await ShelterService.GetAllSheltersAsync();

            _selectedDogId ??= DogId is { } dogId && _dogs.Any(dog => dog.Id == dogId)
                ? dogId
                : _dogs.FirstOrDefault()?.Id;

            await RefreshTransfersAsync();
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

    private async Task RefreshTransfersAsync()
    {
        if (_shelterId is null)
        {
            return;
        }

        var incoming = await DogTransferService.GetIncomingTransfersAsync(_shelterId.Value);
        var outgoing = await DogTransferService.GetOutgoingTransfersAsync(_shelterId.Value);

        _incoming.Clear();
        _incoming.AddRange(incoming.Where(transfer => transfer.Status is DogTransferStatus.Pending or DogTransferStatus.Approved));

        _outgoing.Clear();
        _outgoing.AddRange(outgoing.Where(transfer => transfer.Status is DogTransferStatus.Pending or DogTransferStatus.Approved));

        _history.Clear();
        _history.AddRange(incoming
            .Concat(outgoing)
            .Where(transfer => transfer.Status is DogTransferStatus.Completed or DogTransferStatus.Rejected or DogTransferStatus.Cancelled)
            .DistinctBy(transfer => transfer.Id)
            .OrderByDescending(transfer => transfer.RequestedAtUtc));

        _stats = await DogTransferService.GetTransferStatsAsync(_shelterId.Value);
    }

    private async Task CreateTransferAsync()
    {
        if (_shelterId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Shelter account could not be resolved.", Severity.Error);
            return;
        }

        if (_selectedDogId is null)
        {
            Snackbar.Add("Select a dog before requesting a transfer.", Severity.Warning);
            return;
        }

        if (_destinationShelterId is null)
        {
            Snackbar.Add("Select a destination shelter.", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_reason))
        {
            Snackbar.Add("Reason is required.", Severity.Warning);
            return;
        }

        _isSaving = true;
        try
        {
            await DogTransferService.CreateTransferRequestAsync(
                _shelterId.Value,
                _currentUserId,
                new DogTransferCreateRequest(
                    _selectedDogId.Value,
                    _destinationShelterId.Value,
                    _priority,
                    _reason,
                    _sourceNotes));

            Snackbar.Add("Transfer request created.", Severity.Success);
            _destinationShelterId = null;
            _priority = DogTransferPriority.Normal;
            _reason = null;
            _sourceNotes = null;
            await RefreshTransfersAsync();
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

    private async Task ViewTransferAsync(int transferId)
    {
        if (_shelterId is null)
        {
            return;
        }

        try
        {
            var transfer = await DogTransferService.GetTransferDetailsAsync(transferId, _shelterId.Value);
            var parameters = new DialogParameters<DogTransferDetailsDialog>
            {
                { dialog => dialog.Transfer, transfer },
                { dialog => dialog.ShowAdminNotes, false }
            };
            var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
            await DialogService.ShowAsync<DogTransferDetailsDialog>("Transfer details", parameters, options);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task ApproveAsync(int transferId)
    {
        var notes = await AskForNotesAsync(
            "Approve transfer",
            "Add optional response notes for the source shelter.",
            "Approve",
            Color.Success);
        if (notes is null)
        {
            return;
        }

        await RunTransferActionAsync(
            () => DogTransferService.ApproveTransferAsync(transferId, _shelterId!.Value, _currentUserId!, new DogTransferDecisionRequest(notes)),
            "Transfer request approved.");
    }

    private async Task RejectAsync(int transferId)
    {
        var notes = await AskForNotesAsync(
            "Reject transfer",
            "Explain why this transfer cannot be accepted right now.",
            "Reject",
            Color.Error);
        if (notes is null)
        {
            return;
        }

        await RunTransferActionAsync(
            () => DogTransferService.RejectTransferAsync(transferId, _shelterId!.Value, _currentUserId!, new DogTransferDecisionRequest(notes)),
            "Transfer request rejected.");
    }

    private async Task CancelAsync(int transferId)
    {
        var notes = await AskForNotesAsync(
            "Cancel transfer",
            "Add optional cancellation notes.",
            "Cancel transfer",
            Color.Default);
        if (notes is null)
        {
            return;
        }

        await RunTransferActionAsync(
            () => DogTransferService.CancelTransferAsync(transferId, _shelterId, _currentUserId!, false, new DogTransferDecisionRequest(notes)),
            "Transfer request cancelled.");
    }

    private async Task CompleteAsync(int transferId)
    {
        var notes = await AskForNotesAsync(
            "Complete transfer",
            "Add optional handover notes before moving the dog to the destination shelter.",
            "Complete transfer",
            Color.Primary);
        if (notes is null)
        {
            return;
        }

        await RunTransferActionAsync(
            () => DogTransferService.CompleteTransferAsync(transferId, _shelterId, _currentUserId!, false, new DogTransferCompleteRequest(notes)),
            "Transfer completed and dog shelter updated.");
        if (_shelterId is not null)
        {
            _dogs = (await DogService.GetDogsForShelterAsync(_shelterId.Value))
                .OrderBy(dog => dog.Name)
                .ToList();
        }
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
            await RefreshTransfersAsync();
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
