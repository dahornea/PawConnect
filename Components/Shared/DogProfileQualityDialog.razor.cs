using Microsoft.AspNetCore.Components;
using MudBlazor;
using PawConnect.Services;

namespace PawConnect.Components.Shared;

public partial class DogProfileQualityDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public DogProfileQualityResult? Result { get; set; }

    private bool HasRewrite =>
        Result?.SuggestedRewrite is not null &&
        (!string.IsNullOrWhiteSpace(Result.SuggestedRewrite.Description) ||
         !string.IsNullOrWhiteSpace(Result.SuggestedRewrite.BehaviorDescription));

    private void Close()
    {
        MudDialog.Close();
    }

    private void ApplyRewrite()
    {
        if (Result?.SuggestedRewrite is null)
        {
            MudDialog.Close();
            return;
        }

        MudDialog.Close(DialogResult.Ok(Result.SuggestedRewrite));
    }

    private string GetSourceLabel()
    {
        return Result?.UsedAi == true
            ? "AI-assisted review with PawConnect safety checks"
            : "Local PawConnect profile checks";
    }

    private static Color GetScoreColor(int score)
    {
        return score switch
        {
            >= 80 => Color.Success,
            >= 60 => Color.Warning,
            _ => Color.Error
        };
    }

    private static Color GetSeverityColor(DogProfileQualitySeverity severity)
    {
        return severity switch
        {
            DogProfileQualitySeverity.High => Color.Error,
            DogProfileQualitySeverity.Medium => Color.Warning,
            DogProfileQualitySeverity.Low => Color.Info,
            _ => Color.Default
        };
    }

    private static string FormatCategory(DogProfileQualityCategory category)
    {
        return category.ToString()
            .Replace("Missing", "Missing ", StringComparison.Ordinal)
            .Replace("Info", " info", StringComparison.Ordinal)
            .Replace("Overconfident", "Overconfident ", StringComparison.Ordinal)
            .Replace("Potentially", "Potentially ", StringComparison.Ordinal);
    }
}
