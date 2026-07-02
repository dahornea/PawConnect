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

public partial class AdminUsers
{
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IAdopterProfileService AdopterProfileService { get; set; } = default!;
    [Inject] private IExportService ExportService { get; set; } = default!;
    [Inject] private IBrowserFileDownloadService FileDownloadService { get; set; } = default!;
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private List<UserRow> _users = [];
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isExporting;
    private string? _error;
    private UserEditModel? _editUser;
    private MudForm? _userForm;

    protected override async Task OnInitializedAsync()
    {
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            _users = [];
            var users = await UserManager.Users.OrderBy(u => u.Email).ToListAsync();
            foreach (var user in users)
            {
                var roles = await UserManager.GetRolesAsync(user);
                var adopterProfile = await AdopterProfileService.GetProfileForUserAsync(user.Id);
                _users.Add(new UserRow(
                    user.Id,
                    user.Email,
                    user.FullName,
                    user.UserName,
                    user.PhoneNumber,
                    roles.Count == 0 ? ["No role"] : roles.ToList(),
                    adopterProfile is not null,
                    adopterProfile?.ProfileImageUrl,
                    adopterProfile?.FullName,
                    adopterProfile?.City,
                    adopterProfile?.PhoneNumber));
            }
        }
        catch
        {
            _error = "User data could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void EditUser(UserRow user)
    {
        _editUser = new UserEditModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber
        };
    }

    private void CancelEdit()
    {
        _editUser = null;
    }

    private async Task ExportUsersCsvAsync()
    {
        await ExportAsync(() => ExportService.GenerateUsersCsvAsync());
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

    private async Task SaveUserAsync()
    {
        if (_editUser is null)
        {
            return;
        }

        if (_userForm is not null)
        {
            await _userForm.ValidateAsync();
            if (!_userForm.IsValid)
            {
                return;
            }
        }

        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(_editUser.Email))
        {
            Snackbar.Add("Email must be a valid email address.", Severity.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_editUser.PhoneNumber) &&
            !new System.ComponentModel.DataAnnotations.PhoneAttribute().IsValid(_editUser.PhoneNumber))
        {
            Snackbar.Add("Phone number must be valid.", Severity.Warning);
            return;
        }

        _isSaving = true;

        try
        {
            var user = await UserManager.FindByIdAsync(_editUser.Id);
            if (user is null)
            {
                Snackbar.Add("User was not found.", Severity.Error);
                return;
            }

            var normalizedEmail = _editUser.Email?.Trim();
            var duplicateUser = string.IsNullOrWhiteSpace(normalizedEmail) ? null : await UserManager.FindByEmailAsync(normalizedEmail);
            if (duplicateUser is not null && duplicateUser.Id != user.Id)
            {
                Snackbar.Add("Another user already uses this email address.", Severity.Warning);
                return;
            }

            user.FullName = string.IsNullOrWhiteSpace(_editUser.FullName) ? null : _editUser.FullName.Trim();
            user.Email = normalizedEmail;
            user.NormalizedEmail = UserManager.NormalizeEmail(normalizedEmail);
            user.PhoneNumber = string.IsNullOrWhiteSpace(_editUser.PhoneNumber) ? null : _editUser.PhoneNumber.Trim();

            var result = await UserManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                Snackbar.Add("Could not save user profile. Please check the form and try again.", Severity.Error);
                return;
            }

            await AuditLogService.LogAsync(
                AuditActions.UserUpdatedByAdmin,
                "ApplicationUser",
                user.Id,
                $"User profile/contact information was updated by an admin for {user.Email}.");
            Snackbar.Add("User profile saved.", Severity.Success);
            _editUser = null;
            await LoadUsersAsync();
        }
        catch
        {
            Snackbar.Add("Could not save user profile. Please try again.", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private sealed record UserRow(
        string Id,
        string? Email,
        string? FullName,
        string? UserName,
        string? PhoneNumber,
        List<string> Roles,
        bool HasAdopterProfile,
        string? ProfileImageUrl,
        string? ProfileFullName,
        string? ProfileCity,
        string? ProfilePhoneNumber);

    private sealed class UserEditModel
    {
        public string Id { get; set; } = string.Empty;

        public string? FullName { get; set; }

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }
    }
}

