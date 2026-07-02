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

public partial class ShelterRequestDetailsDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public ShelterRegistrationRequest? Request { get; set; }

    private void Close()
    {
        MudDialog.Close();
    }

    private static bool HasMapLocation(ShelterRegistrationRequest request)
    {
        return request.Latitude.HasValue && request.Longitude.HasValue;
    }

    private static string FormatMapLocation(ShelterRegistrationRequest request)
    {
        return HasMapLocation(request)
            ? "Map location selected"
            : "No map location provided";
    }
}


