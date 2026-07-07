using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminDogs
{
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IExportService ExportService { get; set; } = default!;
    [Inject] private IBrowserFileDownloadService FileDownloadService { get; set; } = default!;
    [Inject] private IDogSearchEmbeddingService DogSearchEmbeddingService { get; set; } = default!;
    [Inject] private IDogProfileCompletenessService DogProfileCompletenessService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private List<Dog> _dogs = [];
    private IReadOnlyDictionary<int, DogProfileCompletenessDto> _completenessByDog = new Dictionary<int, DogProfileCompletenessDto>();
    private DogProfileCompletenessSummaryDto? _completenessSummary;
    private bool _isLoading = true;
    private bool _isDeleting;
    private bool _isHistoryLoading;
    private bool _isExporting;
    private bool _isRebuildingSearchIndex;
    private string? _searchIndexMessage;
    private Severity _searchIndexSeverity = Severity.Info;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _dogs = await DogService.GetAllDogsForAdminAsync();
            RefreshCompleteness();
        }
        catch
        {
            _error = "Dog data could not be loaded. Check migrations and database connection.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ExportDogsCsvAsync()
    {
        await ExportAsync(() => ExportService.GenerateDogsCsvAsync());
    }

    private async Task RebuildDogSearchIndexAsync()
    {
        _isRebuildingSearchIndex = true;

        try
        {
            var result = await DogSearchEmbeddingService.RebuildDogSearchIndexAsync();
            if (!result.IsConfigured)
            {
                _searchIndexSeverity = Severity.Warning;
                _searchIndexMessage = "OpenAI embeddings are disabled or not configured.";
                Snackbar.Add(_searchIndexMessage, Severity.Warning);
            }
            else if (result.HasFailures && result.GeneratedRows == 0)
            {
                _searchIndexSeverity = Severity.Error;
                _searchIndexMessage = "Could not rebuild dog search index. Please check OpenAI configuration.";
                Snackbar.Add(_searchIndexMessage, Severity.Error);
            }
            else if (result.GeneratedRows == 0 && result.SkippedUnchanged > 0 && result.Failed == 0)
            {
                _searchIndexSeverity = Severity.Info;
                _searchIndexMessage = $"Dog search index is already up to date. {result.SkippedUnchanged} dog record(s) checked.";
                Snackbar.Add(_searchIndexMessage, Severity.Info);
            }
            else
            {
                _searchIndexSeverity = result.HasFailures ? Severity.Warning : Severity.Success;
                _searchIndexMessage = result.HasFailures
                    ? $"Dog search index partially rebuilt: {result.Created} created, {result.Updated} updated, {result.SkippedUnchanged} unchanged, {result.Removed} removed, {result.Failed} failed."
                    : $"Dog search index rebuilt successfully. {result.Created} created, {result.Updated} updated, {result.SkippedUnchanged} unchanged, {result.Removed} removed.";
                Snackbar.Add(_searchIndexMessage, result.HasFailures ? Severity.Warning : Severity.Success);
            }
        }
        catch
        {
            Snackbar.Add("Could not rebuild the dog search index right now.", Severity.Error);
        }
        finally
        {
            _isRebuildingSearchIndex = false;
        }
    }

    private async Task ExportAsync(Func<Task<ExportFile>> exportAction)
    {
        _isExporting = true;

        try
        {
            var file = await exportAction();
            await FileDownloadService.DownloadAsync(file);
            Snackbar.Add("Export generated successfully.", Severity.Success);
        }
        catch
        {
            Snackbar.Add("Could not generate export. Please try again.", Severity.Error);
        }
        finally
        {
            _isExporting = false;
        }
    }

    private async Task ConfirmDeleteDogAsync(Dog dog)
    {
        if (!await ConfirmAsync(
            "Delete dog",
            "Are you sure you want to delete this dog? This action cannot easily be undone.",
            "Delete",
            Color.Error,
            Icons.Material.Filled.DeleteForever))
        {
            return;
        }

        await DeleteDogAsync(dog);
    }

    private async Task DeleteDogAsync(Dog dog)
    {
        _isDeleting = true;

        try
        {
            await DogService.DeleteDogForAdminAsync(dog.Id);
            _dogs.Remove(dog);
            RefreshCompleteness();
            Snackbar.Add("Dog deleted.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not delete dog. Please try again.", Severity.Error);
        }
        finally
        {
            _isDeleting = false;
        }
    }

    private async Task LoadStatusHistoryAsync(Dog dog)
    {
        _isHistoryLoading = true;

        try
        {
            var statusHistory = await DogService.GetStatusHistoryForDogAsync(dog.Id);
            var parameters = new DialogParameters
            {
                ["DogName"] = dog.Name,
                ["Histories"] = statusHistory
            };

            await DialogService.ShowAsync<DogStatusHistoryDialog>("Status History", parameters);
        }
        catch
        {
            Snackbar.Add("Status history could not be loaded right now.", Severity.Error);
        }
        finally
        {
            _isHistoryLoading = false;
        }
    }

    private static string? GetImageUrl(Dog dog)
    {
        return DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images);
    }

    private void RefreshCompleteness()
    {
        _completenessByDog = DogProfileCompletenessService.CalculateForDogs(_dogs);
        var results = _completenessByDog.Values.ToList();
        _completenessSummary = new DogProfileCompletenessSummaryDto(
            results.Count,
            results.Count == 0 ? 0 : Math.Round(results.Average(item => item.ScorePercent), 1),
            results.Count(item => item.Label == "Excellent"),
            results.Count(item => item.Label == "Good"),
            results.Count(item => item.Label == "Needs Work"),
            results.Count(item => item.Label == "Incomplete"),
            results
                .Where(item => item.ScorePercent < 70 || item.MissingItems.Any(missing => missing.IsCritical))
                .OrderBy(item => item.ScorePercent)
                .ThenBy(item => item.DogName)
                .Take(8)
                .ToList());
    }

    private bool TryGetCompleteness(int dogId, out DogProfileCompletenessDto completeness)
    {
        return _completenessByDog.TryGetValue(dogId, out completeness!);
    }

    private static Color GetStatusColor(DogStatus status)
    {
        return status switch
        {
            DogStatus.Available => Color.Success,
            DogStatus.Reserved => Color.Warning,
            DogStatus.Adopted => Color.Default,
            DogStatus.InTreatment => Color.Info,
            _ => Color.Default
        };
    }

    private static string FormatAge(Dog dog) => DogAgeFormatter.Format(dog);

    private static bool HasSuccessStory(Dog dog)
    {
        return !string.IsNullOrWhiteSpace(dog.SuccessStoryText) || dog.AdoptedAt.HasValue;
    }

    private static string GetAdminDogDetailsUrl(int dogId)
    {
        return $"/dogs/{dogId}?returnUrl={Uri.EscapeDataString("/admin/dogs")}";
    }

    private async Task OpenSuccessStoryDialogAsync(Dog dog)
    {
        var parameters = new DialogParameters
        {
            ["DogName"] = dog.Name,
            ["Breed"] = DogBreedFormatter.Format(dog),
            ["ShelterName"] = dog.Shelter?.Name,
            ["AdoptedAt"] = dog.AdoptedAt,
            ["SuccessStoryText"] = dog.SuccessStoryText
        };

        await DialogService.ShowAsync<SuccessStoryDetailsDialog>("Success Story", parameters);
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
}

