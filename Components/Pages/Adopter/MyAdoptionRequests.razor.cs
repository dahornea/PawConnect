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

namespace PawConnect.Components.Pages.Adopter;

public partial class MyAdoptionRequests
{
    [Inject] private IAdoptionRequestService AdoptionRequestService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ILogger<MyAdoptionRequests> Logger { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private List<AdoptionRequest> _requests = [];
    private bool _isLoading = true;
    private string? _error;
    private string? _currentUserId;
    private int PendingCount => _requests.Count(r => r.Status == AdoptionRequestStatus.Pending);
    private int VisitConfirmedCount => _requests.Count(r => r.Status == AdoptionRequestStatus.VisitConfirmed);
    private int AcceptedCount => _requests.Count(r => r.Status == AdoptionRequestStatus.Accepted);
    private int ClosedCount => _requests.Count(r => r.Status is AdoptionRequestStatus.Rejected or AdoptionRequestStatus.Cancelled);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _isLoading = true;
            _error = null;
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                _error = "Current adopter account could not be found.";
                return;
            }

            _requests = await AdoptionRequestService.GetRequestsForAdopterAsync(_currentUserId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not load adoption requests for adopter {AdopterId}.", _currentUserId);
            _error = "Adoption requests could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task CancelRequestAsync(AdoptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Current adopter account could not be found.", Severity.Error);
            return;
        }

        if (!await ConfirmAsync(
            "Cancel adoption request",
            "Are you sure you want to cancel this adoption request?",
            "Cancel Request",
            Color.Warning,
            Icons.Material.Filled.Cancel))
        {
            return;
        }

        try
        {
            await AdoptionRequestService.CancelRequestAsync(request.Id, _currentUserId);
            request.Status = AdoptionRequestStatus.Cancelled;
            request.UpdatedAt = DateTime.UtcNow;
            Snackbar.Add("Adoption request cancelled.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not cancel the request. Please try again.", Severity.Error);
        }
    }

    private static string? GetImageUrl(Dog? dog)
    {
        return dog is null ? null : DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images);
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

    private static Color GetVisitStatusColor(AdoptionVisitStatus status)
    {
        return status switch
        {
            AdoptionVisitStatus.Requested => Color.Warning,
            AdoptionVisitStatus.Confirmed => Color.Success,
            AdoptionVisitStatus.Completed => Color.Info,
            AdoptionVisitStatus.Cancelled => Color.Default,
            _ => Color.Default
        };
    }

    private static string GetRequestStatusLabel(AdoptionRequestStatus status)
    {
        return status switch
        {
            AdoptionRequestStatus.Pending => "Pending",
            AdoptionRequestStatus.VisitConfirmed => "Visit confirmed",
            AdoptionRequestStatus.Accepted => "Accepted",
            AdoptionRequestStatus.Rejected => "Rejected",
            AdoptionRequestStatus.Cancelled => "Cancelled",
            _ => FormatEnumLabel(status.ToString())
        };
    }

    private static string GetVisitStatusLabel(AdoptionVisitStatus status)
    {
        return status switch
        {
            AdoptionVisitStatus.Requested => "Visit requested",
            AdoptionVisitStatus.Confirmed => "Visit confirmed",
            AdoptionVisitStatus.Completed => "Visit completed",
            AdoptionVisitStatus.Cancelled => "Visit cancelled",
            AdoptionVisitStatus.NotScheduled => "Not scheduled",
            _ => FormatEnumLabel(status.ToString())
        };
    }

    private static string GetRequestProgressText(AdoptionRequest request)
    {
        return request.Status switch
        {
            AdoptionRequestStatus.Pending => "Waiting for shelter review.",
            AdoptionRequestStatus.VisitConfirmed => "Your visit has been confirmed.",
            AdoptionRequestStatus.Accepted => "The adoption was completed after the visit.",
            AdoptionRequestStatus.Rejected => "This request was rejected.",
            AdoptionRequestStatus.Cancelled => "This request was cancelled.",
            _ => string.Empty
        };
    }

    private static string FormatVisitDateTime(DateTime? visitDateTime)
    {
        return VisitSchedulingHelper.FormatVisitDateTime(visitDateTime);
    }

    private static string FormatDateTime(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
    }

    private static string FormatHoursAlone(int? hoursAlonePerDay)
    {
        return hoursAlonePerDay.HasValue ? $"{hoursAlonePerDay.Value} hour(s) per day" : "-";
    }

    private static string GetRequestSummary(AdoptionRequest request)
    {
        var reason = string.IsNullOrWhiteSpace(request.ReasonForAdoption)
            ? "No reason provided."
            : request.ReasonForAdoption.Trim();

        return TrimText(reason, 180);
    }

    private static string TrimText(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..Math.Max(0, maxLength - 3)]}...";
    }

    private static bool CanCancelRequest(AdoptionRequest request)
    {
        return request.Status == AdoptionRequestStatus.Pending;
    }

    private static bool ShouldShowVisitStatus(AdoptionVisitStatus status)
    {
        return status != AdoptionVisitStatus.NotScheduled;
    }

    private static Severity GetProgressSeverity(AdoptionRequest request)
    {
        return request.Status switch
        {
            AdoptionRequestStatus.Pending => Severity.Info,
            AdoptionRequestStatus.VisitConfirmed => Severity.Success,
            AdoptionRequestStatus.Accepted => Severity.Success,
            AdoptionRequestStatus.Rejected => Severity.Error,
            AdoptionRequestStatus.Cancelled => Severity.Warning,
            _ => Severity.Info
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

    private static string GetDogStatusLabel(DogStatus status)
    {
        return FormatEnumLabel(status.ToString());
    }

    private static string DisplayOrFallback(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string GetDogImageLabel(AdoptionRequest request)
    {
        return $"{DisplayOrFallback(request.Dog?.Name, "Dog")} image";
    }

    private static string GetDogImageAlt(AdoptionRequest request)
    {
        return DisplayOrFallback(request.Dog?.Name, "Dog");
    }

    private static string FormatEnumLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var words = new List<string>();
        var start = 0;
        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && !char.IsWhiteSpace(value[i - 1]))
            {
                words.Add(value[start..i]);
                start = i;
            }
        }

        words.Add(value[start..]);
        var label = string.Join(" ", words).ToLowerInvariant();
        return char.ToUpperInvariant(label[0]) + label[1..];
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
