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

public partial class ShelterDetails
{
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private ILogger<ShelterDetails> Logger { get; set; } = default!;

[Parameter]
    public int Id { get; set; }

    private PawConnect.Entities.Shelter? _shelter;
    private bool _isLoading = true;
    private string? _error;

    private List<Dog> PublicDogs => _shelter?.Dogs
        .Where(d => d.Status is DogStatus.Available or DogStatus.Reserved)
        .OrderBy(d => d.Name)
        .ToList() ?? [];

    private int PublicDogCount => PublicDogs.Count;

    private string? GoogleMapsUrl => BuildGoogleMapsUrl(_shelter);

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            _shelter = await ShelterService.GetPublicShelterDetailsAsync(Id);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Shelter details could not be loaded for shelter id {ShelterId}.", Id);
            _error = "Shelter details could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static Color GetStatusColor(DogStatus status)
    {
        return status switch
        {
            DogStatus.Available => Color.Success,
            DogStatus.Reserved => Color.Warning,
            DogStatus.InTreatment => Color.Info,
            DogStatus.Adopted => Color.Success,
            _ => Color.Default
        };
    }

    private static string? BuildGoogleMapsUrl(PawConnect.Entities.Shelter? shelter)
    {
        if (shelter is null)
        {
            return null;
        }

        if (shelter.Latitude.HasValue && shelter.Longitude.HasValue)
        {
            var latitude = shelter.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var longitude = shelter.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return $"https://www.google.com/maps/search/?api=1&query={latitude},{longitude}";
        }

        var addressQuery = string.Join(", ", new[] { shelter.Address, shelter.City }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(addressQuery)
            ? null
            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(addressQuery)}";
    }
}

