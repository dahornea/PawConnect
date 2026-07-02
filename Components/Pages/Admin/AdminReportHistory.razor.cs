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

public partial class AdminReportHistory
{
    [Inject] private IReportHistoryService ReportHistoryService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private const string SuccessFilter = "Successful";
    private const string FailedFilter = "Failed";

    private static readonly IReadOnlyList<string> ReportTypeFilters =
    [
        ReportHistoryTypes.ShelterSummaryReport,
        ReportHistoryTypes.AdoptionRequestReport,
        ReportHistoryTypes.AdoptionStatusReport,
        ReportHistoryTypes.LowStockResourceReport,
        ReportHistoryTypes.ShelterRegistrationRequestReport,
        ReportHistoryTypes.CsvExport,
        ReportHistoryTypes.PdfExport
    ];

    private List<ReportHistory> _reports = [];
    private bool _isLoading = true;
    private string? _reportTypeFilter;
    private string? _statusFilter;

    protected override async Task OnInitializedAsync()
    {
        await LoadReportsAsync();
    }

    private async Task LoadReportsAsync()
    {
        _isLoading = true;

        try
        {
            _reports = await ReportHistoryService.GetAdminReportHistoryAsync(_reportTypeFilter, GetStatusFilter());
        }
        catch
        {
            Snackbar.Add("Report history could not be loaded right now.", Severity.Error);
            _reports = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ClearFiltersAsync()
    {
        _reportTypeFilter = null;
        _statusFilter = null;
        await LoadReportsAsync();
    }

    private bool? GetStatusFilter()
    {
        return _statusFilter switch
        {
            SuccessFilter => true,
            FailedFilter => false,
            _ => null
        };
    }

    private static string FormatReportType(string reportType)
    {
        return reportType switch
        {
            ReportHistoryTypes.ShelterSummaryReport => "Shelter Summary Report",
            ReportHistoryTypes.AdminPlatformSummaryReport => "Admin Platform Summary Report",
            ReportHistoryTypes.AdoptionRequestReport => "Adoption Request Report",
            ReportHistoryTypes.AdoptionStatusReport => "Adoption Status Report",
            ReportHistoryTypes.LowStockResourceReport => "Low Stock Resource Report",
            ReportHistoryTypes.ShelterRegistrationRequestReport => "Shelter Application Report",
            ReportHistoryTypes.CsvExport => "CSV Export",
            ReportHistoryTypes.PdfExport => "PDF Export",
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

    private static string Truncate(string value)
    {
        return value.Length <= 80 ? value : $"{value[..80]}...";
    }
}

