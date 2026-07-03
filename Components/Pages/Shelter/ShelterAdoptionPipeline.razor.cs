using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Shelter;

public partial class ShelterAdoptionPipeline
{
    [Inject] private IAdoptionRequestService AdoptionRequestService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<ShelterAdoptionPipeline> Logger { get; set; } = default!;

    private ShelterAdoptionPipelineDto? _pipeline;
    private string? _currentUserId;
    private string? _error;
    private bool _isLoading = true;
    private bool _isUpdating;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await LoadPipelineAsync();
    }

    private async Task LoadPipelineAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                _error = "Current shelter account could not be found.";
                return;
            }

            _pipeline = await AdoptionRequestService.GetShelterPipelineAsync(_currentUserId);
        }
        catch (InvalidOperationException ex)
        {
            _error = ex.Message;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not load adoption pipeline for shelter account {UserId}.", _currentUserId);
            _error = "Adoption pipeline could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OpenRequestDetailsAsync(AdoptionPipelineCardDto card)
    {
        try
        {
            var requestDetails = await AdoptionRequestService.GetByIdAsync(card.AdoptionRequestId);
            if (requestDetails is null)
            {
                Snackbar.Add("Adoption request could not be found.", Severity.Warning);
                return;
            }

            var parameters = new DialogParameters
            {
                ["Request"] = requestDetails,
                ["ReturnUrl"] = "/shelter/adoption-pipeline"
            };

            await DialogService.ShowAsync<AdoptionRequestDetailsDialog>("Adoption Request", parameters);
        }
        catch
        {
            Snackbar.Add("Could not open adoption request details right now.", Severity.Error);
        }
    }

    private async Task ConfirmVisitAsync(AdoptionPipelineCardDto card)
    {
        if (!await ConfirmAsync(
            "Confirm shelter visit",
            "Confirm this adopter's preferred visit time? The adopter will receive an email with a calendar invitation.",
            "Confirm Visit",
            Color.Primary,
            Icons.Material.Filled.EventAvailable))
        {
            return;
        }

        await RunPipelineActionAsync(
            () => AdoptionRequestService.ConfirmPipelineVisitAsync(card.AdoptionRequestId, _currentUserId!),
            "Visit confirmed and adopter notified.",
            "Could not confirm the visit right now.");
    }

    private async Task RejectRequestAsync(AdoptionPipelineCardDto card)
    {
        if (!await ConfirmAsync(
            "Reject adoption request",
            "Reject this adoption request? The adopter may be notified by email.",
            "Reject",
            Color.Error,
            Icons.Material.Filled.Cancel))
        {
            return;
        }

        await RunPipelineActionAsync(
            () => AdoptionRequestService.RejectPipelineRequestAsync(card.AdoptionRequestId, _currentUserId!),
            "Request rejected and adopter notified.",
            "Could not reject the request right now.");
    }

    private async Task MarkAsAdoptedAsync(AdoptionPipelineCardDto card)
    {
        if (!await ConfirmAsync(
            "Finalize adoption",
            "Mark this request as adopted after the confirmed visit? This will move the dog to Adopted.",
            "Finalize",
            Color.Success,
            Icons.Material.Filled.Pets))
        {
            return;
        }

        await RunPipelineActionAsync(
            () => AdoptionRequestService.MarkPipelineRequestAsAdoptedAsync(card.AdoptionRequestId, _currentUserId!),
            "Adoption finalized and adopter notified.",
            "Could not finalize the adoption right now.");
    }

    private async Task RunPipelineActionAsync(Func<Task> action, string successMessage, string failureMessage)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Current shelter account could not be found.", Severity.Error);
            return;
        }

        _isUpdating = true;

        try
        {
            await action();
            Snackbar.Add(successMessage, Severity.Success);
            await LoadPipelineAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not update adoption pipeline action.");
            Snackbar.Add(failureMessage, Severity.Error);
        }
        finally
        {
            _isUpdating = false;
        }
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

    private static Color GetColumnColor(AdoptionPipelineStage stage)
    {
        return stage switch
        {
            AdoptionPipelineStage.Pending => Color.Warning,
            AdoptionPipelineStage.VisitConfirmed => Color.Info,
            AdoptionPipelineStage.Accepted => Color.Success,
            AdoptionPipelineStage.Closed => Color.Default,
            _ => Color.Default
        };
    }

    private static Color GetRequestStatusColor(AdoptionRequestStatus status)
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

    private static string FormatRequestStatus(AdoptionRequestStatus status)
    {
        return status switch
        {
            AdoptionRequestStatus.Pending => "Pending",
            AdoptionRequestStatus.VisitConfirmed => "Visit confirmed",
            AdoptionRequestStatus.Accepted => "Accepted",
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
            AdoptionVisitStatus.Confirmed => Color.Info,
            AdoptionVisitStatus.Completed => Color.Success,
            AdoptionVisitStatus.Cancelled => Color.Default,
            _ => Color.Default
        };
    }

    private static string FormatVisitStatus(AdoptionVisitStatus status)
    {
        return status switch
        {
            AdoptionVisitStatus.Requested => "Visit requested",
            AdoptionVisitStatus.Confirmed => "Visit confirmed",
            AdoptionVisitStatus.Completed => "Visit completed",
            AdoptionVisitStatus.Cancelled => "Visit cancelled",
            _ => "Not scheduled"
        };
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

    private static string FormatVisitDate(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToLocalTime().ToString("dd MMM, HH:mm")
            : "No visit selected";
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToLocalTime().ToString("dd MMM yyyy");
    }
}
