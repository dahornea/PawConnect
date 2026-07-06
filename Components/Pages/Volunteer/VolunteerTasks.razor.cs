using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Volunteer;

public partial class VolunteerTasks
{
    [Inject] private IVolunteerTaskService VolunteerTaskService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<VolunteerTaskDto> _myTasks = [];
    private readonly List<VolunteerTaskDto> _openTasks = [];
    private VolunteerProfileDto? _profile;
    private VolunteerTaskStatsDto? _stats;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _error;
    private string? _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(authenticationState.User);
            _currentUserId = user?.Id ?? authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(_currentUserId))
            {
                _error = "Current user could not be resolved.";
                return;
            }

            _profile = await VolunteerTaskService.GetVolunteerProfileForUserAsync(_currentUserId);
            if (_profile is null || !_profile.IsActive)
            {
                return;
            }

            await RefreshTasksAsync();
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

    private async Task RefreshTasksAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            return;
        }

        var myTasks = await VolunteerTaskService.GetVolunteerTasksAsync(_currentUserId);
        _myTasks.Clear();
        _myTasks.AddRange(myTasks);

        var openTasks = await VolunteerTaskService.GetOpenTasksForVolunteerAsync(_currentUserId);
        _openTasks.Clear();
        _openTasks.AddRange(openTasks);

        _stats = await VolunteerTaskService.GetTaskStatsAsync(volunteerUserId: _currentUserId);
    }

    private Task AcceptAsync(int taskId) =>
        RunTaskActionAsync(
            () => VolunteerTaskService.AcceptTaskAsync(taskId, _currentUserId!, new VolunteerTaskActionRequest("Accepted from volunteer task board.")),
            "Volunteer task accepted.");

    private Task StartAsync(int taskId) =>
        RunTaskActionAsync(
            () => VolunteerTaskService.StartTaskAsync(taskId, _currentUserId!, new VolunteerTaskActionRequest("Started by volunteer.")),
            "Volunteer task started.");

    private async Task CompleteAsync(int taskId)
    {
        var notes = await AskForNotesAsync("Complete volunteer task", "Add optional completion notes for the shelter.", "Complete", Color.Success);
        if (notes is null)
        {
            return;
        }

        await RunTaskActionAsync(
            () => VolunteerTaskService.CompleteTaskAsync(taskId, _currentUserId!, new VolunteerTaskActionRequest(notes)),
            "Volunteer task completed.");
    }

    private async Task AddCommentAsync(int taskId)
    {
        var notes = await AskForNotesAsync("Add task note", "Add a short note for the shelter team.", "Save note", Color.Info);
        if (notes is null)
        {
            return;
        }

        await RunTaskActionAsync(
            () => VolunteerTaskService.AddTaskCommentAsync(taskId, _currentUserId!, new VolunteerTaskActionRequest(notes)),
            "Task note saved.");
    }

    private async Task ViewTaskAsync(int taskId)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            return;
        }

        try
        {
            var task = await VolunteerTaskService.GetTaskDetailsAsync(taskId, volunteerUserId: _currentUserId);
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

    private async Task RunTaskActionAsync(Func<Task> action, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Volunteer account could not be resolved.", Severity.Error);
            return;
        }

        _isSaving = true;
        try
        {
            await action();
            Snackbar.Add(successMessage, Severity.Success);
            await RefreshTasksAsync();
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
