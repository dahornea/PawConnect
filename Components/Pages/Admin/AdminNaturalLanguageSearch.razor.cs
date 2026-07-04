using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminNaturalLanguageSearch
{
    [Inject] private INaturalLanguageSearchService NaturalLanguageSearchService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private static readonly IReadOnlyList<string> ExampleQueries =
    [
        "Pending requests from last week",
        "Dogs reserved for more than 10 days",
        "Low stock resources",
        "Reports generated this month",
        "Shelters with pending applications"
    ];

    private string _query = string.Empty;
    private NaturalLanguageSearchResult? _result;
    private bool _isSearching;
    private string? _error;

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(_query))
        {
            Snackbar.Add("Enter a natural-language search query first.", Severity.Info);
            return;
        }

        _isSearching = true;
        _error = null;

        try
        {
            var userId = await GetCurrentUserIdAsync();
            if (string.IsNullOrWhiteSpace(userId))
            {
                _error = "The current admin account could not be resolved.";
                return;
            }

            _result = await NaturalLanguageSearchService.SearchAdminAsync(
                new NaturalLanguageSearchRequest(_query, userId));
        }
        catch (InvalidOperationException ex)
        {
            _error = ex.Message;
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            _error = "Natural-language search could not be completed right now.";
            Snackbar.Add(_error, Severity.Error);
        }
        finally
        {
            _isSearching = false;
        }
    }

    private async Task UseExampleAsync(string example)
    {
        _query = example;
        await SearchAsync();
    }

    private Task ClearAsync()
    {
        _query = string.Empty;
        _result = null;
        _error = null;
        return Task.CompletedTask;
    }

    private async Task<string?> GetCurrentUserIdAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        return authState.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static IReadOnlyList<string> GetInterpretationChips(NaturalLanguageSearchInterpretation interpretation)
    {
        var chips = new List<string>
        {
            $"Scope: {FormatEnum(interpretation.Scope)}",
            $"Intent: {FormatEnum(interpretation.Intent)}"
        };

        if (interpretation.RequestStatus.HasValue)
        {
            chips.Add($"Request status: {FormatEnum(interpretation.RequestStatus.Value)}");
        }

        if (interpretation.VisitStatus.HasValue)
        {
            chips.Add($"Visit status: {FormatEnum(interpretation.VisitStatus.Value)}");
        }

        if (interpretation.DogStatus.HasValue)
        {
            chips.Add($"Dog status: {FormatEnum(interpretation.DogStatus.Value)}");
        }

        if (interpretation.ShelterApplicationStatus.HasValue)
        {
            chips.Add($"Application status: {FormatEnum(interpretation.ShelterApplicationStatus.Value)}");
        }

        if (!string.IsNullOrWhiteSpace(interpretation.City))
        {
            chips.Add($"City/location: {interpretation.City}");
        }

        if (!string.IsNullOrWhiteSpace(interpretation.ResourceCategory))
        {
            chips.Add($"Resource category: {interpretation.ResourceCategory}");
        }

        if (interpretation.LowStockOnly)
        {
            chips.Add("Low stock only");
        }

        if (interpretation.NoRequestsOnly)
        {
            chips.Add("No request history");
        }

        if (interpretation.OlderThanDays.HasValue)
        {
            chips.Add($"Older than: {interpretation.OlderThanDays.Value} days");
        }

        if (interpretation.DateRange?.Label is not null)
        {
            chips.Add($"Date range: {interpretation.DateRange.Label}");
        }

        chips.Add($"Limit: {interpretation.Limit}");
        return chips.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Color GetStatusColor(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return Color.Default;
        }

        if (status.Contains("Pending", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Reserved", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Low stock", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Warning;
        }

        if (status.Contains("Available", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Successful", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Accepted", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Success;
        }

        if (status.Contains("Rejected", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Error;
        }

        return Color.Default;
    }

    private static string FormatEnum<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return System.Text.RegularExpressions.Regex.Replace(value.ToString(), "([a-z])([A-Z])", "$1 $2");
    }
}
