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

public partial class AdminActivityLog
{
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private static readonly IReadOnlyList<string> EntityFilters =
    [
        "AdoptionRequest",
        "ApplicationUser",
        "Dog",
        "DogImage",
        "Export",
        "MedicalRecord",
        "ResourceStock",
        "Shelter",
        "ShelterRegistrationRequest"
    ];

    private List<AuditLog> _logs = [];
    private bool _isLoading = true;
    private string? _search;
    private string? _actionFilter;
    private string? _entityFilter;

    protected override async Task OnInitializedAsync()
    {
        await LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        _isLoading = true;

        try
        {
            _logs = await AuditLogService.GetLogsAsync(_actionFilter, _entityFilter, _search, take: 300);
        }
        catch
        {
            Snackbar.Add("Activity log could not be loaded right now.", Severity.Error);
            _logs = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ClearFiltersAsync()
    {
        _search = null;
        _actionFilter = null;
        _entityFilter = null;
        await LoadLogsAsync();
    }

    private static Color GetActionColor(string action)
    {
        if (action.Contains("Deleted", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Rejected", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Error;
        }

        if (action.Contains("Created", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Accepted", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Submitted", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Success;
        }

        if (action.Contains("Report", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Export", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Info;
        }

        return Color.Primary;
    }
}

