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

public partial class AdoptionRequestDetailsDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public AdoptionRequest? Request { get; set; }

    [Parameter]
    public string ReturnUrl { get; set; } = "/admin/adoption-requests";

    private string RequestTitle => Request?.Dog?.Name is { Length: > 0 } dogName
        ? $"Request for {dogName}"
        : "Request review";

    private void Close()
    {
        MudDialog.Close();
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string FormatDogAge(Dog? dog)
    {
        return dog is null ? "-" : DogAgeFormatter.Format(dog);
    }

    private static string FormatVisitDateTime(DateTime? visitDateTime)
    {
        return VisitSchedulingHelper.FormatVisitDateTime(visitDateTime);
    }

    private static string FormatDateTime(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("dd MMM yyyy HH:mm");
    }

    private static string GetAdopterName(ApplicationUser? adopter)
    {
        if (adopter is null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(adopter.FullName)
            ? adopter.Email ?? adopter.UserName ?? string.Empty
            : adopter.FullName;
    }

    private static string FormatYesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static Color GetRequestStatusColor(AdoptionRequestStatus status)
    {
        return status switch
        {
            AdoptionRequestStatus.Pending => Color.Warning,
            AdoptionRequestStatus.VisitConfirmed => Color.Info,
            AdoptionRequestStatus.Accepted => Color.Success,
            AdoptionRequestStatus.Rejected => Color.Error,
            AdoptionRequestStatus.Cancelled => Color.Default,
            _ => Color.Default
        };
    }

    private static string GetRequestStatusText(AdoptionRequestStatus status)
    {
        return status switch
        {
            AdoptionRequestStatus.Pending => "Pending",
            AdoptionRequestStatus.VisitConfirmed => "Visit confirmed",
            AdoptionRequestStatus.Accepted => "Adopted",
            AdoptionRequestStatus.Rejected => "Rejected",
            AdoptionRequestStatus.Cancelled => "Cancelled",
            _ => status.ToString()
        };
    }

    private static string GetVisitStatusText(AdoptionVisitStatus status)
    {
        return status switch
        {
            AdoptionVisitStatus.Requested => "Visit requested",
            AdoptionVisitStatus.Confirmed => "Visit confirmed",
            AdoptionVisitStatus.Completed => "Completed",
            AdoptionVisitStatus.Cancelled => "Cancelled",
            _ => "Not scheduled"
        };
    }

    private static Color GetDogStatusColor(DogStatus status)
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


