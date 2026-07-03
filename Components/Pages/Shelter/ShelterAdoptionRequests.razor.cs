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

public partial class ShelterAdoptionRequests
{
    [Inject] private IAdoptionRequestService AdoptionRequestService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private IExportService ExportService { get; set; } = default!;
    [Inject] private IBrowserFileDownloadService FileDownloadService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ILogger<ShelterAdoptionRequests> Logger { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private List<AdoptionRequest> _requests = [];
    private bool _isLoading = true;
    private bool _isUpdating;
    private bool _isSavingNotes;
    private bool _isExporting;
    private string? _error;
    private int? _shelterId;
    private string? _currentUserId;
    private int? _editingNotesRequestId;
    private string? _internalNotesModel;
    private AdoptionRequest? _selectedRequest;
    private int PendingRequestCount => _requests.Count(r => r.Status == AdoptionRequestStatus.Pending);
    private int VisitConfirmedCount => _requests.Count(r => r.Status == AdoptionRequestStatus.VisitConfirmed);
    private int AcceptedRequestCount => _requests.Count(r => r.Status == AdoptionRequestStatus.Accepted);
    private int RejectedRequestCount => _requests.Count(r => r.Status == AdoptionRequestStatus.Rejected);
    private static bool HasRequestActions(AdoptionRequest request)
    {
        return request.Status is AdoptionRequestStatus.Pending or AdoptionRequestStatus.VisitConfirmed;
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                _error = "Current shelter account could not be found.";
                return;
            }

            var shelter = await ShelterService.GetShelterForUserAsync(_currentUserId);
            if (shelter is null)
            {
                _error = "No shelter profile is linked to this account.";
                return;
            }

            _shelterId = shelter.Id;
            _requests = await AdoptionRequestService.GetRequestsForShelterAsync(shelter.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not load adoption requests for shelter account {UserId}.", _currentUserId);
            _error = "Adoption requests could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ConfirmVisitAsync(AdoptionRequest request)
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        var slotId = await SelectVisitSlotAsync(request.Id);
        if (!slotId.HasValue)
        {
            return;
        }

        _isUpdating = true;

        try
        {
            await AdoptionRequestService.ConfirmVisitAsync(request.Id, _shelterId.Value, _currentUserId, slotId.Value);
            _requests = await AdoptionRequestService.GetRequestsForShelterAsync(_shelterId.Value);
            Snackbar.Add("Visit confirmed and adopter notified.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not accept the request. Please try again.", Severity.Error);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private async Task<int?> SelectVisitSlotAsync(int adoptionRequestId)
    {
        var parameters = new DialogParameters
        {
            ["AdoptionRequestId"] = adoptionRequestId
        };

        var dialog = await DialogService.ShowAsync<ConfirmVisitSlotDialog>("Select visit slot", parameters);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: int slotId } ? slotId : null;
    }

    private async Task MarkAsAdoptedAsync(AdoptionRequest request)
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        if (!await ConfirmAsync(
            "Mark dog as adopted",
            "Mark this request as adopted after the confirmed visit? This will move the dog to Adopted.",
            "Mark as Adopted",
            Color.Success,
            Icons.Material.Filled.Pets))
        {
            return;
        }

        _isUpdating = true;

        try
        {
            await AdoptionRequestService.MarkAsAdoptedAsync(request.Id, _shelterId.Value, _currentUserId);
            var now = DateTime.UtcNow;
            request.Status = AdoptionRequestStatus.Accepted;
            request.VisitStatus = AdoptionVisitStatus.Completed;
            request.UpdatedAt = now;
            if (request.Dog is not null)
            {
                request.Dog.Status = DogStatus.Adopted;
                request.Dog.AdoptedAt ??= now;
            }

            Snackbar.Add("Dog marked as adopted and adopter notified.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not mark the request as adopted. Please try again.", Severity.Error);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private async Task ExportAdoptionRequestsCsvAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        await ExportAsync(() => ExportService.GenerateShelterAdoptionRequestsCsvAsync(_shelterId.Value));
    }

    private async Task ExportAdoptionRequestsPdfAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        await ExportAsync(() => ExportService.GenerateShelterAdoptionRequestsPdfAsync(_shelterId.Value));
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

    private async Task RejectRequestAsync(AdoptionRequest request)
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        if (!await ConfirmAsync(
            "Reject adoption request",
            "Are you sure you want to reject this adoption request? The adopter may be notified by email.",
            "Reject",
            Color.Error,
            Icons.Material.Filled.Cancel))
        {
            return;
        }

        _isUpdating = true;

        try
        {
            await AdoptionRequestService.RejectRequestAsync(request.Id, _shelterId.Value);
            request.Status = AdoptionRequestStatus.Rejected;
            if (request.VisitStatus != AdoptionVisitStatus.NotScheduled)
            {
                request.VisitStatus = AdoptionVisitStatus.Cancelled;
            }
            request.UpdatedAt = DateTime.UtcNow;
            Snackbar.Add("Request rejected and adopter notified.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not reject the request. Please try again.", Severity.Error);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void StartInternalNotesEdit(AdoptionRequest request)
    {
        _selectedRequest = request;
        _editingNotesRequestId = request.Id;
        _internalNotesModel = request.ShelterInternalNotes;
    }

    private void CancelInternalNotesEdit()
    {
        _editingNotesRequestId = null;
        _internalNotesModel = null;
    }

    private async Task SaveInternalNotesAsync(AdoptionRequest request)
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        _isSavingNotes = true;

        try
        {
            await AdoptionRequestService.UpdateShelterInternalNotesAsync(request.Id, _shelterId.Value, _internalNotesModel);
            request.ShelterInternalNotes = string.IsNullOrWhiteSpace(_internalNotesModel) ? null : _internalNotesModel.Trim();
            request.UpdatedAt = DateTime.UtcNow;
            if (_selectedRequest?.Id == request.Id)
            {
                _selectedRequest.ShelterInternalNotes = request.ShelterInternalNotes;
                _selectedRequest.UpdatedAt = request.UpdatedAt;
            }
            CancelInternalNotesEdit();
            Snackbar.Add("Internal shelter notes saved.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not save internal notes. Please try again.", Severity.Error);
        }
        finally
        {
            _isSavingNotes = false;
        }
    }

    private async Task OpenRequestDetailsDialogAsync(AdoptionRequest request)
    {
        CancelInternalNotesEdit();

        try
        {
            var requestDetails = await AdoptionRequestService.GetByIdAsync(request.Id) ?? request;
            var parameters = new DialogParameters
            {
                ["Request"] = requestDetails,
                ["ReturnUrl"] = "/shelter/adoption-requests"
            };

            await DialogService.ShowAsync<AdoptionRequestDetailsDialog>("Adoption Request", parameters);
        }
        catch
        {
            Snackbar.Add("Could not open adoption request details right now.", Severity.Error);
        }
    }

    private void CloseRequestDetails()
    {
        _selectedRequest = null;
        CancelInternalNotesEdit();
    }

    private static string? GetImageUrl(Dog? dog)
    {
        return dog is null ? null : DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images);
    }

    private static string GetAdopterName(ApplicationUser? adopter)
    {
        if (adopter is null)
        {
            return "Unknown adopter";
        }

        return string.IsNullOrWhiteSpace(adopter.FullName)
            ? adopter.Email ?? adopter.UserName ?? "Unknown adopter"
            : $"{adopter.FullName} ({adopter.Email})";
    }

    private static Color GetStatusColor(AdoptionRequestStatus status)
    {
        return status switch
        {
            AdoptionRequestStatus.Pending => Color.Warning,
            AdoptionRequestStatus.VisitConfirmed => Color.Info,
            AdoptionRequestStatus.Accepted => Color.Success,
            AdoptionRequestStatus.Rejected => Color.Error,
            AdoptionRequestStatus.Cancelled => Color.Default,
            _ => Color.Default
        };
    }

    private static string GetRequestStatusText(AdoptionRequestStatus status)
    {
        return status switch
        {
            AdoptionRequestStatus.Pending => "Pending",
            AdoptionRequestStatus.VisitConfirmed => "Visit confirmed",
            AdoptionRequestStatus.Accepted => "Adopted",
            AdoptionRequestStatus.Rejected => "Rejected",
            AdoptionRequestStatus.Cancelled => "Cancelled",
            _ => status.ToString()
        };
    }

    private static Color GetVisitStatusColor(AdoptionVisitStatus status)
    {
        return status switch
        {
            AdoptionVisitStatus.Requested => Color.Warning,
            AdoptionVisitStatus.Confirmed => Color.Default,
            AdoptionVisitStatus.Completed => Color.Info,
            AdoptionVisitStatus.Cancelled => Color.Default,
            _ => Color.Default
        };
    }

    private static string GetVisitStatusText(AdoptionVisitStatus status)
    {
        return status switch
        {
            AdoptionVisitStatus.Requested => "Visit requested",
            AdoptionVisitStatus.Confirmed => "Visit confirmed",
            AdoptionVisitStatus.Completed => "Completed",
            AdoptionVisitStatus.Cancelled => "Cancelled",
            _ => "Not scheduled"
        };
    }

    private static string GetVisitChipText(AdoptionVisitStatus status)
    {
        return status switch
        {
            AdoptionVisitStatus.Requested => "Requested",
            AdoptionVisitStatus.Confirmed => "Confirmed",
            AdoptionVisitStatus.Completed => "Completed",
            AdoptionVisitStatus.Cancelled => "Cancelled",
            _ => "Not scheduled"
        };
    }

    private static string FormatVisitDateTime(DateTime? visitDateTime)
    {
        return VisitSchedulingHelper.FormatVisitDateTime(visitDateTime);
    }

    private static string FormatTableVisitDateTime(DateTime? visitDateTime)
    {
        return visitDateTime.HasValue
            ? $"{FormatTableDate(visitDateTime.Value)}, {FormatTableTime(visitDateTime.Value)}"
            : "No visit selected";
    }

    private static bool ShouldShowVisitChip(AdoptionVisitStatus status)
    {
        return status != AdoptionVisitStatus.NotScheduled;
    }

    private static string FormatTableDate(DateTime dateTime)
    {
        var localDateTime = dateTime.ToLocalTime();
        var format = localDateTime.Year == DateTime.Now.Year ? "dd MMM" : "dd MMM yyyy";
        return localDateTime.ToString(format);
    }

    private static string FormatTableTime(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("HH:mm");
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

    private static string FormatYesNo(bool value)
    {
        return value ? "Yes" : "No";
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
