using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminAnalyticsDashboard
{
    private const string Last7Preset = "last-7";
    private const string Last30Preset = "last-30";
    private const string Last90Preset = "last-90";
    private const string ThisMonthPreset = "this-month";
    private const string CustomPreset = "custom";

    [Inject] private IAnalyticsService AnalyticsService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    private AdminAnalyticsDashboardDto? _dashboard;
    private IReadOnlyList<AnalyticsShelterOptionDto> _shelterOptions = [];
    private bool _isLoading = true;
    private string? _error;
    private string _selectedPreset = Last30Preset;
    private int? _selectedShelterId;
    private DateTime? _customStart = DateTime.Today.AddDays(-29);
    private DateTime? _customEnd = DateTime.Today;

    protected override Task OnInitializedAsync()
    {
        return LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _isLoading = true;
            _error = null;
            var userId = await GetCurrentUserIdAsync();
            if (string.IsNullOrWhiteSpace(userId))
            {
                _error = "You must be signed in as an administrator to view analytics.";
                return;
            }

            var range = BuildRange();
            _dashboard = await AnalyticsService.GetAdminAnalyticsAsync(range, _selectedShelterId, userId);
            _shelterOptions = _dashboard.ShelterOptions;
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnPresetChangedAsync(string preset)
    {
        _selectedPreset = preset;
        await LoadAsync();
    }

    private async Task OnShelterChangedAsync(int? shelterId)
    {
        _selectedShelterId = shelterId;
        await LoadAsync();
    }

    private async Task OnCustomStartChangedAsync(DateTime? value)
    {
        _customStart = value;
        if (_selectedPreset == CustomPreset)
        {
            await LoadAsync();
        }
    }

    private async Task OnCustomEndChangedAsync(DateTime? value)
    {
        _customEnd = value;
        if (_selectedPreset == CustomPreset)
        {
            await LoadAsync();
        }
    }

    private async Task<string?> GetCurrentUserIdAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private AnalyticsDateRange BuildRange()
    {
        var now = DateTime.UtcNow;
        return _selectedPreset switch
        {
            Last7Preset => AnalyticsDateRange.LastDays(7, now),
            Last90Preset => AnalyticsDateRange.LastDays(90, now),
            ThisMonthPreset => BuildThisMonthRange(now),
            CustomPreset => BuildCustomRange(),
            _ => AnalyticsDateRange.LastDays(30, now)
        };
    }

    private static AnalyticsDateRange BuildThisMonthRange(DateTime utcNow)
    {
        var start = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new AnalyticsDateRange(start, start.AddMonths(1), "This month");
    }

    private AnalyticsDateRange BuildCustomRange()
    {
        if (_customStart is null || _customEnd is null)
        {
            throw new ArgumentException("Select both a start and end date for custom analytics.");
        }

        var start = DateTime.SpecifyKind(_customStart.Value.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(_customEnd.Value.Date.AddDays(1), DateTimeKind.Utc);
        return new AnalyticsDateRange(start, end, $"{start:dd MMM yyyy} - {_customEnd.Value:dd MMM yyyy}");
    }
}
