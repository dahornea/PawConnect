using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Shelter;

public partial class ShelterAvailability
{
    private const string AllFilter = "All";
    private const string AvailableFilter = "Available";
    private const string BookedFilter = "Booked";
    private const string CancelledFilter = "Cancelled";

    [Inject] private IShelterAvailabilityService ShelterAvailabilityService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<ShelterAvailability> Logger { get; set; } = default!;

    private List<ShelterAvailabilitySlotDto> _slots = [];
    private int? _shelterId;
    private string? _currentUserId;
    private string? _error;
    private bool _isLoading = true;
    private bool _isLoadingSlots;
    private bool _isSaving;
    private DateTime? _fromDate = DateTime.Today;
    private DateTime? _toDate = DateTime.Today.AddDays(14);
    private DateTime? _newSlotDate = DateTime.Today.AddDays(1);
    private string _newSlotStartTime = "10:00";
    private string _newSlotEndTime = "11:00";
    private string? _newSlotNotes;
    private string _statusFilter = AllFilter;

    private List<ShelterAvailabilitySlotDto> FilteredSlots =>
        _slots.Where(MatchesStatusFilter).ToList();

    private IEnumerable<IGrouping<DateTime, ShelterAvailabilitySlotDto>> GroupedSlots =>
        FilteredSlots.GroupBy(slot => slot.StartTime.Date).OrderBy(group => group.Key);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                _error = "Current shelter account could not be found.";
                return;
            }

            var shelter = await ShelterService.GetShelterForUserAsync(_currentUserId);
            if (shelter is null)
            {
                _error = "No shelter profile is linked to this account.";
                return;
            }

            _shelterId = shelter.Id;
            await LoadSlotsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not load shelter availability for user {UserId}.", _currentUserId);
            _error = "Visit availability could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadSlotsAsync()
    {
        if (_shelterId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            return;
        }

        _isLoadingSlots = true;

        try
        {
            var from = _fromDate ?? DateTime.Today;
            var to = _toDate ?? from.AddDays(14);
            if (to < from)
            {
                (from, to) = (to, from);
            }

            _slots = await ShelterAvailabilityService.GetShelterSlotsAsync(
                _shelterId.Value,
                from,
                to,
                _currentUserId);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not refresh shelter availability slots.");
            Snackbar.Add("Availability slots could not be refreshed.", Severity.Error);
        }
        finally
        {
            _isLoadingSlots = false;
        }
    }

    private async Task CreateSlotAsync()
    {
        if (_shelterId is null || string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        if (!_newSlotDate.HasValue ||
            !TryParseTime(_newSlotStartTime, out var startTime) ||
            !TryParseTime(_newSlotEndTime, out var endTime))
        {
            Snackbar.Add("Please choose a date and valid start/end times such as 10:00.", Severity.Warning);
            return;
        }

        var start = _newSlotDate.Value.Date.Add(startTime);
        var end = _newSlotDate.Value.Date.Add(endTime);
        _isSaving = true;

        try
        {
            await ShelterAvailabilityService.CreateSlotAsync(
                new CreateShelterAvailabilitySlotRequest(_shelterId.Value, start, end, _newSlotNotes),
                _currentUserId);

            _newSlotNotes = null;
            Snackbar.Add("Availability slot created.", Severity.Success);
            await LoadSlotsAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create shelter availability slot.");
            Snackbar.Add("Could not create the slot. Please try again.", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task CancelSlotAsync(ShelterAvailabilitySlotDto slot)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Current shelter account could not be found.", Severity.Error);
            return;
        }

        _isSaving = true;

        try
        {
            await ShelterAvailabilityService.CancelSlotAsync(slot.Id, _currentUserId);
            Snackbar.Add("Availability slot cancelled.", Severity.Success);
            await LoadSlotsAsync();
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not cancel availability slot {SlotId}.", slot.Id);
            Snackbar.Add("Could not cancel the slot. Please try again.", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private bool MatchesStatusFilter(ShelterAvailabilitySlotDto slot)
    {
        return _statusFilter switch
        {
            AvailableFilter => !slot.IsBooked && !slot.IsCancelled && !slot.IsPast,
            BookedFilter => slot.IsBooked,
            CancelledFilter => slot.IsCancelled,
            _ => true
        };
    }

    private static bool TryParseTime(string? value, out TimeSpan time)
    {
        return TimeSpan.TryParseExact(value?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out time) ||
            TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time);
    }

    private static bool CanCancelSlot(ShelterAvailabilitySlotDto slot)
    {
        return !slot.IsBooked && !slot.IsCancelled && !slot.IsPast;
    }

    private static string FormatSlotTime(ShelterAvailabilitySlotDto slot)
    {
        return $"{slot.StartTime:HH:mm}-{slot.EndTime:HH:mm}";
    }

    private static string FormatBookedText(ShelterAvailabilitySlotDto slot)
    {
        var dog = string.IsNullOrWhiteSpace(slot.BookedDogName) ? "Unknown dog" : slot.BookedDogName;
        var adopter = string.IsNullOrWhiteSpace(slot.BookedAdopterName) ? "Unknown adopter" : slot.BookedAdopterName;
        return $"Booked for {dog} · {adopter}";
    }

    private static string GetSlotStatus(ShelterAvailabilitySlotDto slot)
    {
        if (slot.IsCancelled)
        {
            return "Cancelled";
        }

        if (slot.IsBooked)
        {
            return "Booked";
        }

        return slot.IsPast ? "Past" : "Available";
    }

    private static Color GetSlotColor(ShelterAvailabilitySlotDto slot)
    {
        if (slot.IsCancelled)
        {
            return Color.Default;
        }

        if (slot.IsBooked)
        {
            return Color.Info;
        }

        return slot.IsPast ? Color.Default : Color.Success;
    }
}
