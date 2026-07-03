using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Services;

namespace PawConnect.Components.Shared;

public partial class ConfirmVisitSlotDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public int AdoptionRequestId { get; set; }

    [Inject] private IShelterAvailabilityService ShelterAvailabilityService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    private List<ShelterAvailabilitySlotDto> _slots = [];
    private bool _isLoading = true;
    private string? _error;
    private int _selectedSlotId;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                _error = "Current account could not be found.";
                return;
            }

            _slots = await ShelterAvailabilityService.GetAvailableSlotsForAdoptionRequestAsync(AdoptionRequestId, currentUserId);
            _selectedSlotId = _slots.FirstOrDefault()?.Id ?? 0;
        }
        catch (InvalidOperationException ex)
        {
            _error = ex.Message;
        }
        catch
        {
            _error = "Available slots could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Confirm()
    {
        if (_selectedSlotId == 0)
        {
            return;
        }

        MudDialog.Close(DialogResult.Ok(_selectedSlotId));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private static string FormatSlot(ShelterAvailabilitySlotDto slot)
    {
        var culture = CultureInfo.InvariantCulture;
        return $"{slot.StartTime:ddd, dd MMM yyyy} · {slot.StartTime.ToString("HH:mm", culture)}-{slot.EndTime.ToString("HH:mm", culture)}";
    }
}
