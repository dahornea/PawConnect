using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminAdoptionRequests
{
    [Inject] private IAdoptionRequestService AdoptionRequestService { get; set; } = default!;
    [Inject] private IExportService ExportService { get; set; } = default!;
    [Inject] private IBrowserFileDownloadService FileDownloadService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

private List<AdoptionRequest> _requests = [];
    private bool _isLoading = true;
    private bool _isExporting;
    private string? _error;
    private int PendingRequestCount => _requests.Count(r => r.Status == AdoptionRequestStatus.Pending);
    private int VisitConfirmedCount => _requests.Count(r => r.Status == AdoptionRequestStatus.VisitConfirmed);
    private int AcceptedRequestCount => _requests.Count(r => r.Status == AdoptionRequestStatus.Accepted);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _requests = await AdoptionRequestService.GetAllAsync();
        }
        catch
        {
            _error = "Adoption request data could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ExportAdoptionRequestsCsvAsync()
    {
        await ExportAsync(() => ExportService.GenerateAdoptionRequestsCsvAsync());
    }

    private async Task ExportAdoptionRequestsPdfAsync()
    {
        await ExportAsync(() => ExportService.GenerateAdoptionRequestsPdfAsync());
    }

    private async Task OpenRequestDetailsAsync(AdoptionRequest request)
    {
        try
        {
            var requestDetails = await AdoptionRequestService.GetByIdAsync(request.Id) ?? request;
            var parameters = new DialogParameters
            {
                ["Request"] = requestDetails,
                ["ReturnUrl"] = "/admin/adoption-requests"
            };

            await DialogService.ShowAsync<AdoptionRequestDetailsDialog>("Adoption Request", parameters);
        }
        catch
        {
            Snackbar.Add("Could not open adoption request details right now.", Severity.Error);
        }
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

    private static string FormatYesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static string DisplayOrFallback(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}

