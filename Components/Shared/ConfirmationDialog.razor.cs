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

public partial class ConfirmationDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public string Title { get; set; } = "Confirm action";

    [Parameter]
    public string Message { get; set; } = "Are you sure you want to continue?";

    [Parameter]
    public string ConfirmText { get; set; } = "Confirm";

    [Parameter]
    public string CancelText { get; set; } = "Cancel";

    [Parameter]
    public Color ConfirmColor { get; set; } = Color.Primary;

    [Parameter]
    public Color IconColor { get; set; } = Color.Warning;

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.HelpOutline;

    private void Confirm()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}


