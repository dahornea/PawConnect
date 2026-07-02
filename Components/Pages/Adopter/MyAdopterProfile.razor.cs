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

namespace PawConnect.Components.Pages.Adopter;

public partial class MyAdopterProfile
{
    [Inject] private IAdopterProfileService AdopterProfileService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private AdopterProfile _profile = new();
    private MudForm? _form;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;
    private string? _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(authState.User);
            _currentUserId = user?.Id;

            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                _error = "Current adopter account could not be found.";
                return;
            }

            _profile = await AdopterProfileService.GetProfileForUserAsync(_currentUserId)
                ?? new AdopterProfile
                {
                    FullName = user?.FullName ?? string.Empty,
                    City = string.Empty,
                    HousingType = HousingType.Apartment
                };
        }
        catch
        {
            _error = "Adopter profile could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Current adopter account could not be found.", Severity.Error);
            return;
        }

        if (_form is not null)
        {
            await _form.ValidateAsync();
            if (!_form.IsValid)
            {
                return;
            }
        }

        _isSaving = true;

        try
        {
            await AdopterProfileService.CreateOrUpdateProfileAsync(_currentUserId, _profile);
            Snackbar.Add("Profile saved successfully.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not save profile. Please check the form and try again.", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }
}

