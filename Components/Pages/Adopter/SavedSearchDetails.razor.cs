using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Adopter;

public partial class SavedSearchDetails
{
    [Parameter] public int SavedSearchId { get; set; }

    [Inject] private ISavedDogSearchService SavedDogSearchService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private SavedDogSearchDetailsDto? _details;
    private bool _isLoading = true;
    private string? _error;
    private string? _userId;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _userId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);
        await LoadAsync();
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
            _details = await SavedDogSearchService.GetSavedSearchDetailsAsync(SavedSearchId, _userId);
            if (_details is null)
            {
                _error = "Saved search was not found.";
            }
        }
        catch
        {
            _error = "Saved search matches could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        _details = await SavedDogSearchService.EvaluateSavedSearchAsync(SavedSearchId, _userId);
        Snackbar.Add("Matches refreshed.", Severity.Success);
    }

    private async Task ToggleAlertsAsync()
    {
        if (string.IsNullOrWhiteSpace(_userId) || _details is null)
        {
            return;
        }

        await SavedDogSearchService.SetAlertsAsync(SavedSearchId, _userId, !_details.Search.AlertsEnabled);
        Snackbar.Add(!_details.Search.AlertsEnabled ? "Alerts enabled." : "Alerts paused.", Severity.Success);
        await LoadAsync();
    }

    private async Task MarkSeenAsync(int matchId)
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        await SavedDogSearchService.MarkMatchAsSeenAsync(matchId, _userId);
        await LoadAsync();
    }

    private async Task DismissAsync(int matchId)
    {
        if (string.IsNullOrWhiteSpace(_userId))
        {
            return;
        }

        await SavedDogSearchService.DismissMatchAsync(matchId, _userId);
        Snackbar.Add("Match dismissed.", Severity.Success);
        await LoadAsync();
    }

    private static Color GetStatusColor(DogStatus status)
    {
        return status switch
        {
            DogStatus.Available => Color.Success,
            DogStatus.Reserved => Color.Warning,
            DogStatus.InTreatment => Color.Info,
            _ => Color.Default
        };
    }
}
