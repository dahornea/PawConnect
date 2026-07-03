using Microsoft.AspNetCore.Components;
using MudBlazor;
using PawConnect.Services;

namespace PawConnect.Components.Shared;

public partial class CopilotExplanationDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public CopilotExplanationDto? Explanation { get; set; }

    private string Subtitle => Explanation is null
        ? "Copilot explanation"
        : $"{Explanation.DogName} - based on PawConnect profile evidence";

    private static string FormatConstraint(AdoptionCopilotConstraint constraint)
    {
        return string.IsNullOrWhiteSpace(constraint.Label)
            ? constraint.Value
            : string.IsNullOrWhiteSpace(constraint.Value)
                ? constraint.Label
                : $"{constraint.Label}: {constraint.Value}";
    }

    private static IReadOnlyList<string> GetCleanValues(IReadOnlyList<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void Close()
    {
        MudDialog.Close();
    }
}
