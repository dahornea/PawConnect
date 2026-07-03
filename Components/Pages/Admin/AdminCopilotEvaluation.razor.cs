using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminCopilotEvaluation
{
    [Inject] private ICopilotEvaluationService CopilotEvaluationService { get; set; } = default!;
    [Inject] private IBrowserFileDownloadService FileDownloadService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private List<CopilotEvaluationCase> _cases = [];
    private List<CopilotEvaluationResult> _results = [];
    private string? _selectedCaseId;
    private string? _currentUserId;
    private string? _error;
    private double _passThreshold = 70;
    private bool _isLoading = true;
    private bool _isRunning;

    private bool CanRunSelected => !_isLoading && !_isRunning && !string.IsNullOrWhiteSpace(_selectedCaseId);

    private CopilotEvaluationSummary Summary
    {
        get
        {
            if (_results.Count == 0)
            {
                return new CopilotEvaluationSummary(_cases.Count, 0, 0, 0, 0);
            }

            return new CopilotEvaluationSummary(
                _results.Count,
                _results.Count(result => result.Passed),
                _results.Count(result => !result.Passed),
                _results.Average(result => result.Comparison.AccuracyPercent),
                _results.Average(result => result.DurationMs));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            _cases = (await CopilotEvaluationService.GetCasesAsync()).ToList();
            _selectedCaseId = _cases.FirstOrDefault()?.Id;
        }
        catch
        {
            _error = "Copilot evaluation cases could not be loaded.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RunSelectedAsync()
    {
        var evaluationCase = _cases.FirstOrDefault(item => item.Id == _selectedCaseId);
        if (evaluationCase is null)
        {
            Snackbar.Add("Select an evaluation case first.", Severity.Info);
            return;
        }

        await RunAsync(async userId =>
        {
            var result = await CopilotEvaluationService.RunCaseAsync(evaluationCase, userId, _passThreshold);
            var existingIndex = _results.FindIndex(item => item.Case.Id == evaluationCase.Id);
            if (existingIndex >= 0)
            {
                _results[existingIndex] = result;
            }
            else
            {
                _results.Insert(0, result);
            }
        });
    }

    private async Task RunAllAsync()
    {
        await RunAsync(async userId =>
        {
            _results = (await CopilotEvaluationService.RunAllAsync(userId, _passThreshold)).ToList();
        });
    }

    private async Task RunAsync(Func<string, Task> action)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            _error = "Current admin account could not be resolved.";
            return;
        }

        _isRunning = true;
        _error = null;

        try
        {
            await action(_currentUserId);
        }
        catch
        {
            _error = "Copilot evaluation could not be completed right now.";
        }
        finally
        {
            _isRunning = false;
        }
    }

    private async Task ExportJsonAsync()
    {
        if (_results.Count == 0)
        {
            Snackbar.Add("Run at least one evaluation before exporting.", Severity.Info);
            return;
        }

        var file = CopilotEvaluationService.BuildJsonExport(_results);
        await FileDownloadService.DownloadAsync(file);
        Snackbar.Add("Copilot evaluation export generated.", Severity.Success);
    }

    private static Color GetStatusColor(CopilotEvaluationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return Color.Error;
        }

        return result.Passed ? Color.Success : Color.Warning;
    }

    private static string FormatPercent(double value)
    {
        return $"{value:0.#}%";
    }

    private static string FormatDuration(double milliseconds)
    {
        return milliseconds < 1000
            ? $"{milliseconds:0} ms"
            : $"{milliseconds / 1000:0.0} s";
    }

    private static string FormatValues(IReadOnlyList<string> values)
    {
        return values.Count == 0
            ? "-"
            : string.Join(", ", values);
    }
}
