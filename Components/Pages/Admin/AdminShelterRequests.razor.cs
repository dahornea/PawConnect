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

public partial class AdminShelterRequests
{
    [Inject] private IShelterRegistrationRequestService ShelterRegistrationRequestService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IExportService ExportService { get; set; } = default!;
    [Inject] private IBrowserFileDownloadService FileDownloadService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private List<ShelterRegistrationRequest> _requests = [];
    private bool _isLoading = true;
    private bool _isExporting;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _requests = await ShelterRegistrationRequestService.GetAllAsync();
        }
        catch
        {
            _error = "Shelter applications could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ExportShelterRequestsCsvAsync()
    {
        await ExportAsync(() => ExportService.GenerateShelterRequestsCsvAsync());
    }

    private async Task ExportShelterRequestsPdfAsync()
    {
        await ExportAsync(() => ExportService.GenerateShelterRequestsPdfAsync());
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

    private void ViewRequest(ShelterRegistrationRequest request)
    {
        var parameters = new DialogParameters
        {
            ["Request"] = request
        };

        DialogService.ShowAsync<ShelterRequestDetailsDialog>("Shelter Application", parameters);
    }

    private async Task AcceptAsync(int requestId)
    {
        if (!await ConfirmAsync("Accept shelter application?", "Accepting creates a Shelter user account and linked shelter profile."))
        {
            return;
        }

        try
        {
            var adminId = await GetCurrentUserIdAsync();
            await ShelterRegistrationRequestService.AcceptRequestAsync(requestId, adminId);
            Snackbar.Add("Shelter application accepted. Shelter account created.", Severity.Success);
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not accept the shelter application. Please try again.", Severity.Error);
        }
    }

    private async Task RejectAsync(int requestId)
    {
        if (!await ConfirmAsync("Reject shelter application?", "Rejecting keeps the application for history but does not create a shelter account.", Color.Error))
        {
            return;
        }

        try
        {
            var adminId = await GetCurrentUserIdAsync();
            await ShelterRegistrationRequestService.RejectRequestAsync(requestId, adminId);
            Snackbar.Add("Shelter application rejected.", Severity.Success);
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not reject the shelter application. Please try again.", Severity.Error);
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message, Color confirmColor = Color.Primary)
    {
        var parameters = new DialogParameters
        {
            ["Title"] = title,
            ["Message"] = message,
            ["ConfirmText"] = confirmColor == Color.Error ? "Reject" : "Accept",
            ["CancelText"] = "Cancel",
            ["ConfirmColor"] = confirmColor,
            ["Icon"] = Icons.Material.Filled.HelpOutline
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>(title, parameters);
        var result = await dialog.Result;
        return result is not null && !result.Canceled;
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(authState.User);
        return user?.Id ?? string.Empty;
    }

    private int CountStatus(ShelterRegistrationRequestStatus status)
    {
        return _requests.Count(r => r.Status == status);
    }

    private static Color GetStatusColor(ShelterRegistrationRequestStatus status)
    {
        return status switch
        {
            ShelterRegistrationRequestStatus.Pending => Color.Warning,
            ShelterRegistrationRequestStatus.Accepted => Color.Success,
            ShelterRegistrationRequestStatus.Rejected => Color.Error,
            _ => Color.Default
        };
    }
}

