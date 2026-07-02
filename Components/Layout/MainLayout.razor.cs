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

namespace PawConnect.Components.Layout;

public partial class MainLayout
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IAdopterProfileService AdopterProfileService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

private bool _drawerOpen = true;
    private string? _displayName;
    private string? _displayEmail;
    private string _currentUserIcon = Icons.Material.Filled.AccountCircle;

    private string CurrentUserIcon => _currentUserIcon;

    private readonly MudTheme _theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2F6F5E",
            Secondary = "#C9812D",
            Tertiary = "#6A8F7A",
            Success = "#2E7D32",
            Warning = "#C9812D",
            Error = "#B91C1C",
            Info = "#256D85",
            AppbarBackground = "#2F6F5E",
            Background = "#F3F6F0",
            Surface = "#FFFEFB",
            DrawerBackground = "#FFFEFB",
            DrawerText = "#294139",
            TextPrimary = "#1F2F2A",
            TextSecondary = "#66756F"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px"
        }
    };

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    protected override async Task OnInitializedAsync()
    {
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        await LoadUserDisplayAsync(await AuthenticationStateProvider.GetAuthenticationStateAsync());
    }

    private async void OnAuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
    {
        await LoadUserDisplayAsync(await authenticationStateTask);
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadUserDisplayAsync(AuthenticationState authState)
    {
        var principal = authState.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            _displayName = null;
            _displayEmail = null;
            _currentUserIcon = Icons.Material.Filled.AccountCircle;
            return;
        }

        try
        {
            var user = await UserManager.GetUserAsync(principal);
            _displayEmail = user?.Email ?? principal.Identity.Name;
            _displayName = user?.FullName;
            _currentUserIcon = Icons.Material.Filled.AccountCircle;

            if (user is null)
            {
                _displayName = _displayEmail;
                return;
            }

            if (principal.IsInRole(IdentitySeedData.AdopterRole))
            {
                var profile = await AdopterProfileService.GetProfileForUserAsync(user.Id);
                _displayName = string.IsNullOrWhiteSpace(profile?.FullName)
                    ? FirstNonEmpty(user.FullName, _displayEmail)
                    : profile.FullName;
                _currentUserIcon = Icons.Material.Filled.Person;
            }
            else if (principal.IsInRole(IdentitySeedData.ShelterRole))
            {
                var shelter = await ShelterService.GetShelterForUserAsync(user.Id);
                _displayName = string.IsNullOrWhiteSpace(shelter?.Name)
                    ? FirstNonEmpty(user.FullName, _displayEmail)
                    : shelter.Name;
                _currentUserIcon = Icons.Material.Filled.Business;
            }
            else if (principal.IsInRole(IdentitySeedData.AdminRole))
            {
                _displayName = FirstNonEmpty(user.FullName, "Admin", _displayEmail);
                _currentUserIcon = Icons.Material.Filled.AdminPanelSettings;
            }
            else
            {
                _displayName = FirstNonEmpty(user.FullName, _displayEmail);
            }
        }
        catch
        {
            _displayEmail = principal.Identity.Name;
            _displayName = _displayEmail;
            _currentUserIcon = Icons.Material.Filled.AccountCircle;
        }
    }

    private string GetDisplayName(string? fallbackName)
    {
        return FirstNonEmpty(_displayName, _displayEmail, fallbackName, "Signed in");
    }

    private string? GetDisplayEmail(string? fallbackEmail)
    {
        var email = FirstNonEmpty(_displayEmail, fallbackEmail);
        var name = GetDisplayName(fallbackEmail);

        return string.Equals(email, name, StringComparison.OrdinalIgnoreCase) ? null : email;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private async Task ConfirmLogoutAsync()
    {
        var parameters = new DialogParameters
        {
            ["Title"] = "Log out?",
            ["Message"] = "Are you sure you want to log out of PawConnect?",
            ["ConfirmText"] = "Log out",
            ["CancelText"] = "Cancel",
            ["ConfirmColor"] = Color.Primary,
            ["IconColor"] = Color.Primary,
            ["Icon"] = Icons.Material.Filled.Logout
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Log out?", parameters);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            NavigationManager.NavigateTo("/Account/Logout?returnUrl=%3FloggedOut%3Dtrue", forceLoad: true);
        }
    }

    public void Dispose()
    {
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}

