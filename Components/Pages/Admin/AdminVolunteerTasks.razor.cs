using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminVolunteerTasks
{
    [Inject] private IVolunteerTaskService VolunteerTaskService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<VolunteerTaskDto> _tasks = [];
    private List<Entities.Shelter> _shelters = [];
    private VolunteerTaskStatsDto? _stats;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;
    private string? _currentUserId;
    private VolunteerTaskStatus? _statusFilter;
    private VolunteerTaskCategory? _categoryFilter;
    private VolunteerTaskPriority? _priorityFilter;
    private int? _shelterFilter;
    private string? _search;

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        try
        {
            var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(authenticationState.User);
            _currentUserId = user?.Id ?? authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier);
            _shelters = await ShelterService.GetAllSheltersAsync();
            await LoadTasksAsync();
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

    private async Task LoadTasksAsync()
    {
        var filter = new VolunteerTaskFilterDto(
            _statusFilter,
            _categoryFilter,
            _priorityFilter,
            _shelterFilter,
            search: _search);

        var tasks = await VolunteerTaskService.GetAdminTasksAsync(filter);
        _tasks.Clear();
        _tasks.AddRange(tasks);
        _stats = await VolunteerTaskService.GetTaskStatsAsync();
    }

    private async Task ViewTaskAsync(int taskId)
    {
        try
        {
            var task = await VolunteerTaskService.GetTaskDetailsAsync(taskId, isAdmin: true);
            if (task is null)
            {
                Snackbar.Add("Volunteer task was not found.", Severity.Warning);
                return;
            }

            var parameters = new DialogParameters<VolunteerTaskDetailsDialog>
            {
                { dialog => dialog.Task, task }
            };
            var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
            await DialogService.ShowAsync<VolunteerTaskDetailsDialog>("Volunteer task details", parameters, options);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task CancelAsync(int taskId)
    {
        var notes = await AskForNotesAsync("Cancel volunteer task", "Add optional admin notes before cancelling this task.", "Cancel task", Color.Default);
        if (notes is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Admin user could not be resolved.", Severity.Error);
            return;
        }

        _isSaving = true;
        try
        {
            await VolunteerTaskService.CancelTaskAsync(taskId, null, _currentUserId, true, new VolunteerTaskActionRequest(notes));
            Snackbar.Add("Volunteer task cancelled.", Severity.Success);
            await LoadTasksAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<string?> AskForNotesAsync(string title, string message, string confirmText, Color confirmColor)
    {
        var parameters = new DialogParameters<DogTransferDecisionDialog>
        {
            { dialog => dialog.Message, message },
            { dialog => dialog.ConfirmText, confirmText },
            { dialog => dialog.ConfirmColor, confirmColor }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<DogTransferDecisionDialog>(title, parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: string notes } ? notes : null;
    }

    private static string FormatDateTime(DateTime value) => value.ToLocalTime().ToString("dd MMM yyyy HH:mm");

    private static string FormatTime(DateTime value) => value.ToLocalTime().ToString("HH:mm");

    private static string FormatCategory(VolunteerTaskCategory category) => category switch
    {
        VolunteerTaskCategory.DogWalking => "Dog walking",
        VolunteerTaskCategory.MedicalVisitSupport => "Medical visit support",
        VolunteerTaskCategory.AdoptionEventSupport => "Adoption event support",
        _ => category.ToString()
    };

    private static string FormatStatus(VolunteerTaskStatus status) => status switch
    {
        VolunteerTaskStatus.InProgress => "In progress",
        _ => status.ToString()
    };

    private static Color GetStatusColor(VolunteerTaskStatus status) => status switch
    {
        VolunteerTaskStatus.Open => Color.Info,
        VolunteerTaskStatus.Assigned => Color.Warning,
        VolunteerTaskStatus.InProgress => Color.Primary,
        VolunteerTaskStatus.Completed => Color.Success,
        VolunteerTaskStatus.Cancelled => Color.Default,
        _ => Color.Default
    };

    private static Color GetPriorityColor(VolunteerTaskPriority priority) => priority switch
    {
        VolunteerTaskPriority.Low => Color.Default,
        VolunteerTaskPriority.Normal => Color.Info,
        VolunteerTaskPriority.High => Color.Warning,
        VolunteerTaskPriority.Urgent => Color.Error,
        _ => Color.Default
    };
}
