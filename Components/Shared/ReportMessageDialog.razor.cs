using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Services;

namespace PawConnect.Components.Shared;

public partial class ReportMessageDialog
{
    private const int MaxDetailsLength = PawConnect.Services.MessageReportService.MaxDetailsLength;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public int MessageId { get; set; }

    [Inject]
    private IMessageReportService MessageReportService { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    private string? _reason;
    private string? _details;
    private string? _error;
    private bool _isSubmitting;

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_reason))
        {
            _error = "Please select a report reason.";
            return;
        }

        _isSubmitting = true;
        _error = null;

        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                _error = "Current user could not be found.";
                return;
            }

            await MessageReportService.ReportMessageAsync(MessageId, _reason, _details, userId);
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (InvalidOperationException ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
