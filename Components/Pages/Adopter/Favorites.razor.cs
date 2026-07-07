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

public partial class Favorites
{
    [Inject] private IFavoriteDogService FavoriteDogService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private List<FavoriteDog> _favorites = [];
    private bool _isLoading = true;
    private string? _error;
    private string? _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        await LoadFavoritesAsync();
    }

    private async Task LoadFavoritesAsync()
    {
        try
        {
            _isLoading = true;
            _error = null;
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(authState.User);
            _currentUserId = user?.Id;

            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                _error = "Current adopter account could not be found.";
                return;
            }

            _favorites = await FavoriteDogService.GetFavoritesForUserAsync(_currentUserId);
        }
        catch
        {
            _error = "Favorite dogs could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RemoveFavoriteAsync(Dog dog)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Current adopter account could not be found.", Severity.Error);
            return;
        }

        try
        {
            await FavoriteDogService.RemoveFavoriteAsync(_currentUserId, dog.Id);
            _favorites.RemoveAll(f => f.DogId == dog.Id);
            Snackbar.Add("Dog removed from favorites.", Severity.Success);
        }
        catch
        {
            Snackbar.Add("Could not update favorites. Please try again.", Severity.Error);
        }
    }

    private static string? GetImageUrl(Dog dog)
    {
        return DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images);
    }

    private static Color GetStatusColor(DogStatus status)
    {
        return status switch
        {
            DogStatus.Available => Color.Success,
            DogStatus.Reserved => Color.Warning,
            DogStatus.Adopted => Color.Default,
            DogStatus.InTreatment => Color.Info,
            _ => Color.Default
        };
    }
}

