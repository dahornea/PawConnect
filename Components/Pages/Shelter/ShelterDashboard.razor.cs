using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
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

namespace PawConnect.Components.Pages.Shelter;

public partial class ShelterDashboard
{
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IAdoptionRequestService AdoptionRequestService { get; set; } = default!;
    [Inject] private IResourceStockService ResourceStockService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private IShelterSummaryReportService ShelterSummaryReportService { get; set; } = default!;
    [Inject] private IReportHistoryService ReportHistoryService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private bool _isLoading = true;
    private string? _error;
    private int _dogCount;
    private int _availableDogCount;
    private int _pendingRequestCount;
    private int? _shelterId;
    private bool _isSendingReport;
    private List<ResourceStock> _lowStockResources = [];
    private List<ReportHistory> _recentReports = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var shelterId = await GetCurrentShelterIdAsync();
            if (shelterId is null)
            {
                _error = "No shelter profile is linked to this account.";
                return;
            }

            _shelterId = shelterId;
            var dogs = await DogService.GetDogsForShelterAsync(shelterId.Value);
            var requests = await AdoptionRequestService.GetRequestsForShelterAsync(shelterId.Value);
            _lowStockResources = await ResourceStockService.GetLowStockResourcesForShelterAsync(shelterId.Value);
            _recentReports = await ReportHistoryService.GetReportHistoryForShelterAsync(shelterId.Value, 5);

            _dogCount = dogs.Count;
            _availableDogCount = dogs.Count(d => d.Status == DogStatus.Available);
            _pendingRequestCount = requests.Count(r => r.Status == AdoptionRequestStatus.Pending);
        }
        catch
        {
            _error = "Dashboard data could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
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

    private async Task SendSummaryReportAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("No shelter profile is linked to this account.", Severity.Error);
            return;
        }

        try
        {
            _isSendingReport = true;
            await ShelterSummaryReportService.SendShelterSummaryReportAsync(_shelterId.Value);
            _recentReports = await ReportHistoryService.GetReportHistoryForShelterAsync(_shelterId.Value, 5);
            Snackbar.Add("Summary report sent to your shelter email.", Severity.Success);
        }
        catch
        {
            _recentReports = await ReportHistoryService.GetReportHistoryForShelterAsync(_shelterId.Value, 5);
            Snackbar.Add("Could not send summary report. Please try again.", Severity.Error);
        }
        finally
        {
            _isSendingReport = false;
        }
    }

    private static string FormatReportType(string reportType)
    {
        return reportType switch
        {
            ReportHistoryTypes.ShelterSummaryReport => "Shelter Summary Report",
            ReportHistoryTypes.CsvExport => "CSV Export",
            ReportHistoryTypes.PdfExport => "PDF Export",
            ReportHistoryTypes.AdoptionRequestReport => "Adoption Request Report",
            ReportHistoryTypes.AdoptionStatusReport => "Adoption Status Report",
            ReportHistoryTypes.LowStockResourceReport => "Low Stock Resource Report",
            ReportHistoryTypes.ShelterRegistrationRequestReport => "Shelter Application Report",
            _ => reportType
        };
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToLocalTime().ToString("dd MMM yyyy HH:mm");
    }

    private static Color GetReportStatusColor(ReportHistory report)
    {
        if (!report.WasSuccessful)
        {
            return Color.Error;
        }

        return report.SentAt.HasValue ? Color.Success : Color.Info;
    }

    private static string GetReportStatusText(ReportHistory report)
    {
        if (!report.WasSuccessful)
        {
            return "Failed";
        }

        return report.SentAt.HasValue ? "Sent" : "Generated";
    }
}

