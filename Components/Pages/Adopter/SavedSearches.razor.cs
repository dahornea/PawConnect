using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Adopter;

public partial class SavedSearches
{
    [Inject] private ISavedDogSearchService SavedDogSearchService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private IReadOnlyList<SavedDogSearchDto> _searches = [];
    private SavedSearchStatsDto _stats = new(0, 0, 0, null);
    private bool _isLoading = true;
    private string? _error;
    private string? _userId;

    protected override async Task OnInitializedAsync()
    {
        await ResolveUserAsync();
        await LoadAsync();
    }

    private async Task ResolveUserAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            _error = "Current adopter account could not be found.";
            _isLoading = false;
            return;
        }

        _isLoading = true;
        _error = null;
        try
        {
            _searches = await SavedDogSearchService.GetSavedSearchesForAdopterAsync(_userId);
            _stats = await SavedDogSearchService.GetStatsForAdopterAsync(_userId);
        }
        catch
        {
            _error = "Saved searches could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ToggleAlertsAsync(SavedDogSearchDto search, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        try
        {
            await SavedDogSearchService.SetAlertsAsync(search.Id, _userId, enabled);
            Snackbar.Add(enabled ? "Saved search alerts enabled." : "Saved search alerts paused.", Severity.Success);
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
    }

    private async Task EvaluateAsync(int savedSearchId)
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        await SavedDogSearchService.EvaluateSavedSearchAsync(savedSearchId, _userId);
        Snackbar.Add("Saved search matches refreshed.", Severity.Success);
        await LoadAsync();
    }

    private async Task DeleteAsync(SavedDogSearchDto search)
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        var parameters = new DialogParameters
        {
            ["ContentText"] = $"Delete saved search '{search.Name}'? This will remove its match history.",
            ["ConfirmText"] = "Delete",
            ["CancelText"] = "Cancel",
            ["Color"] = Color.Error
        };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Delete saved search", parameters);
        var result = await dialog.Result;
        if (result is null || result.Canceled)
        {
            return;
        }

        await SavedDogSearchService.DeleteSavedSearchAsync(search.Id, _userId);
        Snackbar.Add("Saved search deleted.", Severity.Success);
        await LoadAsync();
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm") : "Not checked yet";
    }
}

