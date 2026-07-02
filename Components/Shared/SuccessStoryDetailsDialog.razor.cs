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

public partial class SuccessStoryDetailsDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public string DogName { get; set; } = string.Empty;

    [Parameter]
    public string Breed { get; set; } = string.Empty;

    [Parameter]
    public string? ShelterName { get; set; }

    [Parameter]
    public DateTime? AdoptedAt { get; set; }

    [Parameter]
    public string? SuccessStoryText { get; set; }

    private static string DisplayOrFallback(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void Close()
    {
        MudDialog.Close();
    }
}


