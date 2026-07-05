using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages;

public partial class NotificationPreferences
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private INotificationPreferenceService PreferenceService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<EditableNotificationPreference> _preferences = [];
    private string? _userId;
    private string? _error;
    private bool _isLoading = true;
    private bool _isSaving;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await LoadPreferencesAsync();
    }

    private async Task LoadPreferencesAsync()
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            _error = "Current user could not be found.";
            _isLoading = false;
            return;
        }

        _isLoading = true;
        _error = null;

        try
        {
            var preferences = await PreferenceService.GetPreferencesAsync(_userId);
            _preferences.Clear();
            _preferences.AddRange(preferences.Select(EditableNotificationPreference.FromDto));
        }
        catch
        {
            _error = "Notification preferences could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        _isSaving = true;
        try
        {
            await PreferenceService.SavePreferencesAsync(
                _userId,
                _preferences
                    .Select(preference => new NotificationPreferenceUpdateDto(
                        preference.NotificationType,
                        preference.InAppEnabled,
                        preference.EmailEnabled))
                    .ToList());

            Snackbar.Add("Notification preferences saved.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Notification preferences could not be saved right now.", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ResetToDefaultsAsync()
    {
        foreach (var preference in _preferences)
        {
            preference.InAppEnabled = preference.DefaultInAppEnabled;
            preference.EmailEnabled = preference.DefaultEmailEnabled;
        }

        await SaveAsync();
    }

    private sealed class EditableNotificationPreference
    {
        public NotificationEventType NotificationType { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public bool InAppEnabled { get; set; }

        public bool EmailEnabled { get; set; }

        public bool DefaultInAppEnabled { get; init; }

        public bool DefaultEmailEnabled { get; init; }

        public static EditableNotificationPreference FromDto(NotificationPreferenceDto dto)
        {
            return new EditableNotificationPreference
            {
                NotificationType = dto.NotificationType,
                DisplayName = dto.DisplayName,
                Description = dto.Description,
                InAppEnabled = dto.InAppEnabled,
                EmailEnabled = dto.EmailEnabled,
                DefaultInAppEnabled = dto.DefaultInAppEnabled,
                DefaultEmailEnabled = dto.DefaultEmailEnabled
            };
        }
    }
}
