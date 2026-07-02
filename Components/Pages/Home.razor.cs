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

namespace PawConnect.Components.Pages;

public partial class Home
{
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

private List<Dog> _featuredDogs = [];
    private bool _isLoadingFeaturedDogs = true;
    private bool _hasRetriedFeaturedDogsLoad;

    [SupplyParameterFromQuery(Name = "loggedOut")]
    private bool LoggedOut { get; set; }

    private bool _logoutMessageShown;

    protected override async Task OnInitializedAsync()
    {
        await LoadFeaturedDogsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _featuredDogs.Count > 0 || _hasRetriedFeaturedDogsLoad)
        {
            return;
        }

        _hasRetriedFeaturedDogsLoad = true;
        await Task.Delay(300);
        await LoadFeaturedDogsAsync();
        StateHasChanged();
    }

    protected override void OnParametersSet()
    {
        if (LoggedOut && !_logoutMessageShown)
        {
            _logoutMessageShown = true;
            Snackbar.Add("You have been logged out successfully.", Severity.Success);
        }
    }

    private async Task LoadFeaturedDogsAsync()
    {
        _isLoadingFeaturedDogs = true;

        try
        {
            _featuredDogs = (await DogService.GetAvailableDogsAsync()).Take(4).ToList();
        }
        catch
        {
            _featuredDogs = [];
        }
        finally
        {
            _isLoadingFeaturedDogs = false;
        }
    }

    private static string? GetDogImageUrl(Dog dog)
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

