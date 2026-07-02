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

namespace PawConnect.Components.Shared;

public partial class ShelterMap
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

private readonly string _mapId = $"shelter-map-{Guid.NewGuid():N}";
    private DotNetObjectReference<ShelterMap>? _dotNetReference;
    private double? _lastRenderedLatitude;
    private double? _lastRenderedLongitude;
    private bool _hasRenderedMap;

    [Parameter]
    public double? Latitude { get; set; }

    [Parameter]
    public EventCallback<double?> LatitudeChanged { get; set; }

    [Parameter]
    public double? Longitude { get; set; }

    [Parameter]
    public EventCallback<double?> LongitudeChanged { get; set; }

    [Parameter]
    public EventCallback<(double Latitude, double Longitude)> CoordinatesChanged { get; set; }

    [Parameter]
    public string ShelterName { get; set; } = "Shelter";

    [Parameter]
    public string? Address { get; set; }

    [Parameter]
    public string? City { get; set; }

    [Parameter]
    public string Height { get; set; } = "340px";

    [Parameter]
    public bool Editable { get; set; }

    private bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;

    private string AddressText => string.Join(", ", new[] { Address, City }.Where(value => !string.IsNullOrWhiteSpace(value)));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!HasCoordinates && !Editable)
        {
            return;
        }

        _dotNetReference ??= DotNetObjectReference.Create(this);

        await JSRuntime.InvokeVoidAsync(
            "pawConnect.renderShelterMap",
            _mapId,
            Latitude,
            Longitude,
            ShelterName,
            AddressText,
            Editable,
            Editable ? _dotNetReference : null);

        _lastRenderedLatitude = Latitude;
        _lastRenderedLongitude = Longitude;
        _hasRenderedMap = true;
    }

    protected override bool ShouldRender()
    {
        return true;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!_hasRenderedMap || (!HasCoordinates && !Editable))
        {
            return;
        }

        if (_lastRenderedLatitude == Latitude && _lastRenderedLongitude == Longitude)
        {
            return;
        }

        await JSRuntime.InvokeVoidAsync(
            "pawConnect.renderShelterMap",
            _mapId,
            Latitude,
            Longitude,
            ShelterName,
            AddressText,
            Editable,
            Editable ? _dotNetReference : null);

        _lastRenderedLatitude = Latitude;
        _lastRenderedLongitude = Longitude;
    }

    [JSInvokable]
    public async Task OnMapCoordinatesChanged(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
        _lastRenderedLatitude = latitude;
        _lastRenderedLongitude = longitude;

        await LatitudeChanged.InvokeAsync(latitude);
        await LongitudeChanged.InvokeAsync(longitude);
        await CoordinatesChanged.InvokeAsync((latitude, longitude));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("pawConnect.disposeShelterMap", _mapId);
        }
        catch
        {
            // The JS runtime can already be disconnected during Blazor Server teardown.
        }

        _dotNetReference?.Dispose();
    }
}

