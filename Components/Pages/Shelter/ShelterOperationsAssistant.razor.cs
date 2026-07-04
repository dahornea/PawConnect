using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Shelter;

public partial class ShelterOperationsAssistant
{
    [Inject] private IShelterOperationsAssistantService AssistantService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    private ShelterOperationsBriefPeriod _period = ShelterOperationsBriefPeriod.Today;
    private ShelterOperationsBriefDto? _brief;
    private bool _isLoading;
    private string? _error;

    private async Task GenerateBriefAsync()
    {
        try
        {
            _isLoading = true;
            _error = null;
            var userId = await GetCurrentUserIdAsync();
            if (string.IsNullOrWhiteSpace(userId))
            {
                _error = "You must be signed in as a shelter account to use the assistant.";
                return;
            }

            _brief = await AssistantService.GenerateBriefAsync(userId, new ShelterOperationsBriefRequest(_period));
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

    private async Task<string?> GetCurrentUserIdAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
    }

    private static string FormatDateTime(DateTime? value)
    {
        return value?.ToLocalTime().ToString("dd MMM yyyy, HH:mm") ?? "No visit time set";
    }

    private static string FormatPriority(ShelterOperationsPriority priority)
    {
        return priority.ToString();
    }

    private static string GetPriorityClass(ShelterOperationsPriority priority)
    {
        return priority switch
        {
            ShelterOperationsPriority.Critical => "priority-critical",
            ShelterOperationsPriority.High => "priority-high",
            ShelterOperationsPriority.Medium => "priority-medium",
            ShelterOperationsPriority.Low => "priority-low",
            _ => "priority-info"
        };
    }

    private static Color GetPriorityColor(ShelterOperationsPriority priority)
    {
        return priority switch
        {
            ShelterOperationsPriority.Critical => Color.Error,
            ShelterOperationsPriority.High => Color.Warning,
            ShelterOperationsPriority.Medium => Color.Info,
            ShelterOperationsPriority.Low => Color.Default,
            _ => Color.Success
        };
    }

    private static Severity GetPrioritySeverity(ShelterOperationsPriority priority)
    {
        return priority switch
        {
            ShelterOperationsPriority.Critical => Severity.Error,
            ShelterOperationsPriority.High => Severity.Warning,
            ShelterOperationsPriority.Medium => Severity.Info,
            _ => Severity.Normal
        };
    }
}
