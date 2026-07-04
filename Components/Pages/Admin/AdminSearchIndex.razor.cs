using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminSearchIndex
{
    private const string AllStatusesValue = "";

    [Inject] private ISearchIndexDashboardService SearchIndexDashboardService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private readonly EmbeddingLifecycleStatus[] _statusOptions = Enum.GetValues<EmbeddingLifecycleStatus>();
    private SearchIndexDashboardSummaryDto? _summary;
    private List<DogEmbeddingStatusDto> _statuses = [];
    private string? _currentUserId;
    private string? _searchTerm;
    private string _statusFilter = AllStatusesValue;
    private string? _error;
    private bool _publicSafeOnly;
    private bool _isLoading = true;
    private bool _isRebuilding;

    private bool CanRebuild => _summary?.EmbeddingsConfigured == true && !_isLoading && !_isRebuilding;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            _summary = await SearchIndexDashboardService.GetSummaryAsync();
            await LoadStatusesAsync();
        }
        catch
        {
            _error = "Semantic search index status could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadStatusesAsync()
    {
        var filter = new EmbeddingLifecycleFilterDto(
            ParseStatusFilter(),
            _searchTerm,
            _publicSafeOnly);

        _statuses = (await SearchIndexDashboardService.GetDogEmbeddingStatusesAsync(filter)).ToList();
    }

    private async Task ApplyFiltersAsync(KeyboardEventArgs _)
    {
        try
        {
            await LoadStatusesAsync();
        }
        catch
        {
            _error = "Search index filters could not be applied.";
        }
    }

    private async Task OnStatusFilterChanged(string value)
    {
        _statusFilter = value;
        await LoadStatusesAsync();
    }

    private async Task OnPublicSafeOnlyChanged(bool value)
    {
        _publicSafeOnly = value;
        await LoadStatusesAsync();
    }

    private async Task RebuildDogAsync(DogEmbeddingStatusDto status)
    {
        await RunRebuildAsync(userId => SearchIndexDashboardService.RebuildDogEmbeddingAsync(status.DogId, userId));
    }

    private async Task RebuildMissingAsync()
    {
        await RunRebuildAsync(userId => SearchIndexDashboardService.RebuildMissingEmbeddingsAsync(userId));
    }

    private async Task RebuildStaleAsync()
    {
        await RunRebuildAsync(userId => SearchIndexDashboardService.RebuildStaleEmbeddingsAsync(userId));
    }

    private async Task ConfirmRebuildAllAsync()
    {
        var confirmed = await ConfirmAsync(
            "Rebuild all embeddings?",
            "This will refresh semantic search embeddings for all public-safe dogs. Keyword fallback remains available during the rebuild.",
            "Rebuild All",
            Color.Primary,
            Icons.Material.Filled.ManageSearch);

        if (confirmed)
        {
            await RunRebuildAsync(userId => SearchIndexDashboardService.RebuildAllPublicSafeEmbeddingsAsync(userId));
        }
    }

    private async Task RunRebuildAsync(Func<string, Task<EmbeddingRebuildResultDto>> action)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Current admin account could not be resolved.", Severity.Error);
            return;
        }

        _isRebuilding = true;
        _error = null;

        try
        {
            var result = await action(_currentUserId);
            Snackbar.Add(result.Message, result.Failed > 0 ? Severity.Warning : Severity.Success);
            _summary = await SearchIndexDashboardService.GetSummaryAsync();
            await LoadStatusesAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            Snackbar.Add("Search index rebuild could not be completed.", Severity.Error);
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private async Task PreviewAsync(DogEmbeddingStatusDto status)
    {
        if (string.IsNullOrWhiteSpace(_currentUserId))
        {
            Snackbar.Add("Current admin account could not be resolved.", Severity.Error);
            return;
        }

        try
        {
            var preview = await SearchIndexDashboardService.GetSearchDocumentPreviewAsync(status.DogId, _currentUserId);
            var parameters = new DialogParameters<SearchDocumentPreviewDialog>
            {
                { dialog => dialog.Preview, preview }
            };
            var options = new DialogOptions
            {
                MaxWidth = MaxWidth.Medium,
                FullWidth = true
            };

            await DialogService.ShowAsync<SearchDocumentPreviewDialog>("Search document", parameters, options);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
    }

    private void OpenDog(int dogId)
    {
        var returnUrl = Uri.EscapeDataString("/admin/search-index");
        NavigationManager.NavigateTo($"/dogs/{dogId}?returnUrl={returnUrl}");
    }

    private EmbeddingLifecycleStatus? ParseStatusFilter()
    {
        return Enum.TryParse<EmbeddingLifecycleStatus>(_statusFilter, out var status)
            ? status
            : null;
    }

    private async Task<bool> ConfirmAsync(string title, string message, string confirmText, Color confirmColor, string icon)
    {
        var parameters = new DialogParameters
        {
            ["Title"] = title,
            ["Message"] = message,
            ["ConfirmText"] = confirmText,
            ["ConfirmColor"] = confirmColor,
            ["IconColor"] = confirmColor,
            ["Icon"] = icon
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>(title, parameters);
        var result = await dialog.Result;
        return result is not null && !result.Canceled;
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToLocalTime().ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture)
            : "-";
    }

    private static string FormatStatus(EmbeddingLifecycleStatus status)
    {
        return status switch
        {
            EmbeddingLifecycleStatus.UpToDate => "Up to date",
            EmbeddingLifecycleStatus.OpenAiDisabled => "OpenAI disabled",
            EmbeddingLifecycleStatus.NotPublicSafe => "Not public-safe",
            _ => status.ToString()
        };
    }

    private static Color GetLifecycleColor(EmbeddingLifecycleStatus status)
    {
        return status switch
        {
            EmbeddingLifecycleStatus.UpToDate => Color.Success,
            EmbeddingLifecycleStatus.Missing => Color.Warning,
            EmbeddingLifecycleStatus.Stale => Color.Warning,
            EmbeddingLifecycleStatus.Failed => Color.Error,
            EmbeddingLifecycleStatus.OpenAiDisabled => Color.Info,
            EmbeddingLifecycleStatus.NotPublicSafe => Color.Default,
            _ => Color.Default
        };
    }

    private static Color GetDogStatusColor(DogStatus status)
    {
        return status switch
        {
            DogStatus.Available => Color.Success,
            DogStatus.Reserved => Color.Warning,
            DogStatus.Adopted => Color.Secondary,
            DogStatus.InTreatment => Color.Info,
            _ => Color.Default
        };
    }
}
