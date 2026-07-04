using Microsoft.AspNetCore.Components;
using MudBlazor;
using PawConnect.Services;

namespace PawConnect.Components.Shared;

public partial class SearchDocumentPreviewDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public SearchDocumentPreviewDto? Preview { get; set; }

    private string ShortHash => Preview?.ContentHash.Length >= 10
        ? Preview.ContentHash[..10]
        : Preview?.ContentHash ?? "-";

    private void Close()
    {
        MudDialog.Close();
    }
}
