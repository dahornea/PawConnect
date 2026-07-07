using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Shelter;

public partial class ManageDogs
{
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private IExportService ExportService { get; set; } = default!;
    [Inject] private ICsvImportService CsvImportService { get; set; } = default!;
    [Inject] private IBrowserFileDownloadService FileDownloadService { get; set; } = default!;
    [Inject] private IDogProfileCompletenessService DogProfileCompletenessService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private List<Dog> _dogs = [];
    private bool _isLoading = true;
    private bool _isDeleting;
    private bool _isExporting;
    private bool _isImporting;
    private string? _error;
    private string? _searchTerm;
    private DogStatus? _statusFilter;
    private DogSize? _sizeFilter;
    private string? _completenessFilter;
    private string _sortOption = "Name";
    private int? _shelterId;
    private IReadOnlyDictionary<int, DogProfileCompletenessDto> _completenessByDog = new Dictionary<int, DogProfileCompletenessDto>();
    private DogProfileCompletenessSummaryDto? _completenessSummary;
    private IBrowserFile? _dogImportFile;
    private CsvImportResult? _importResult;
    private int _dogImportInputKey;
    private const long MaxCsvFileSize = 2 * 1024 * 1024;

    private IEnumerable<Dog> FilteredDogs
    {
        get
        {
            var query = _dogs.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchTerm))
            {
                query = query.Where(d => d.Name.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (_statusFilter.HasValue)
            {
                query = query.Where(d => d.Status == _statusFilter.Value);
            }

            if (_sizeFilter.HasValue)
            {
                query = query.Where(d => d.Size == _sizeFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(_completenessFilter))
            {
                query = query.Where(d => TryGetCompleteness(d.Id, out var completeness) &&
                    completeness.Label == _completenessFilter);
            }

            return _sortOption switch
            {
                "CompletenessAsc" => query.OrderBy(d => TryGetCompleteness(d.Id, out var completeness) ? completeness.ScorePercent : 0).ThenBy(d => d.Name),
                "CompletenessDesc" => query.OrderByDescending(d => TryGetCompleteness(d.Id, out var completeness) ? completeness.ScorePercent : 0).ThenBy(d => d.Name),
                _ => query.OrderBy(d => d.Name)
            };
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _shelterId = await GetCurrentShelterIdAsync();
            if (_shelterId is null)
            {
                _error = "No shelter profile is linked to this account.";
                return;
            }

            _dogs = await DogService.GetDogsForShelterAsync(_shelterId.Value);
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

    private async Task ConfirmDeleteAsync(Dog dog)
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

        await DeleteAsync(dog);
    }

    private async Task ExportDogsCsvAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        await ExportAsync(() => ExportService.GenerateShelterDogsCsvAsync(_shelterId.Value));
    }

    private async Task OnDogCsvSelectedAsync(InputFileChangeEventArgs args)
    {
        _dogImportFile = args.File;
        await PreviewSelectedDogFileAsync();
    }

    private async Task PreviewSelectedDogFileAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        if (_dogImportFile is null)
        {
            Snackbar.Add("Choose a CSV file first.", Severity.Info);
            return;
        }

        if (!IsCsvFile(_dogImportFile))
        {
            Snackbar.Add("Please choose a .csv file.", Severity.Warning);
            ClearDogImportPreview();
            return;
        }

        if (_dogImportFile.Size > MaxCsvFileSize)
        {
            Snackbar.Add("CSV file is too large. Please use a file under 2 MB.", Severity.Warning);
            ClearDogImportPreview();
            return;
        }

        _isImporting = true;

        try
        {
            await using var stream = _dogImportFile.OpenReadStream(MaxCsvFileSize);
            _importResult = await CsvImportService.PreviewShelterDogsImportAsync(stream, _shelterId.Value);
            Snackbar.Add(_importResult.HasErrors ? "CSV contains validation errors. Please review the preview." : "CSV preview is ready.", _importResult.HasErrors ? Severity.Warning : Severity.Success);
        }
        catch
        {
            Snackbar.Add("CSV could not be imported. Please review the validation errors.", Severity.Error);
        }
        finally
        {
            _isImporting = false;
        }
    }

    private async Task ConfirmDogImportAsync()
    {
        if (_shelterId is null || _dogImportFile is null)
        {
            Snackbar.Add("Choose a CSV file first.", Severity.Warning);
            return;
        }

        _isImporting = true;

        try
        {
            await using var stream = _dogImportFile.OpenReadStream(MaxCsvFileSize);
            _importResult = await CsvImportService.ImportShelterDogsAsync(stream, _shelterId.Value);
            if (_importResult.HasErrors)
            {
                Snackbar.Add("CSV contains validation errors. Please fix them and try again.", Severity.Warning);
                return;
            }

            _dogs = await DogService.GetDogsForShelterAsync(_shelterId.Value);
            RefreshCompleteness();
            Snackbar.Add("Dogs imported successfully.", Severity.Success);
            ClearDogImportPreview();
        }
        catch
        {
            Snackbar.Add("CSV could not be imported. Please review the validation errors.", Severity.Error);
        }
        finally
        {
            _isImporting = false;
        }
    }

    private async Task DownloadDogTemplateAsync()
    {
        await FileDownloadService.DownloadAsync(CsvImportService.GenerateShelterDogsTemplate());
    }

    private void ClearDogImportPreview()
    {
        _importResult = null;
        _dogImportFile = null;
        _dogImportInputKey++;
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

    private async Task DeleteAsync(Dog dog)
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        _isDeleting = true;

        try
        {
            await DogService.DeleteDogAsync(dog.Id, _shelterId.Value);
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

    private async Task<int?> GetCurrentShelterIdAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(authState.User);
        if (user is null)
        {
            return null;
        }

        var shelter = await ShelterService.GetShelterForUserAsync(user.Id);
        return shelter?.Id;
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

    private static bool IsCsvFile(IBrowserFile file)
    {
        return Path.GetExtension(file.Name).Equals(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetImportValue(CsvImportRowResult row, string key)
    {
        return row.PreviewData.TryGetValue(key, out var value) ? value : null;
    }

    private static string FormatImportErrors(CsvImportRowResult row)
    {
        return string.Join(" ", row.Errors.Select(error => error.Message));
    }
}
