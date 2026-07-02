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

public partial class Resources
{
    [Inject] private IResourceStockService ResourceStockService { get; set; } = default!;
    [Inject] private IResourceCategoryService ResourceCategoryService { get; set; } = default!;
    [Inject] private IFoodTypeService FoodTypeService { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private IExportService ExportService { get; set; } = default!;
    [Inject] private ICsvImportService CsvImportService { get; set; } = default!;
    [Inject] private IBrowserFileDownloadService FileDownloadService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private List<ResourceStock> _resources = [];
    private List<ResourceCategory> _categories = [];
    private List<FoodType> _foodTypes = [];
    private ResourceStock _model = new();
    private MudForm? _form;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isExporting;
    private bool _isImporting;
    private string? _error;
    private string? _resourceFormError;
    private string? _resourceNameError;
    private string? _resourceCategoryError;
    private string? _foodTypeError;
    private string? _quantityError;
    private string? _unitError;
    private string? _lowStockThresholdError;
    private int? _selectedCategoryId;
    private int? _quantity;
    private int? _lowStockThreshold;
    private int? _shelterId;
    private IBrowserFile? _resourceImportFile;
    private CsvImportResult? _importResult;
    private int _resourceImportInputKey;
    private int _resourceFormKey;
    private const long MaxCsvFileSize = 2 * 1024 * 1024;

    private bool IsFoodCategorySelected =>
        _selectedCategoryId.HasValue &&
        _categories.FirstOrDefault(c => c.Id == _selectedCategoryId.Value)?.Name.Equals("Food", StringComparison.OrdinalIgnoreCase) == true;
    private bool HasResourceNameError => !string.IsNullOrWhiteSpace(_resourceNameError);
    private bool HasResourceCategoryError => !string.IsNullOrWhiteSpace(_resourceCategoryError);
    private bool HasFoodTypeError => !string.IsNullOrWhiteSpace(_foodTypeError);
    private bool HasQuantityError => !string.IsNullOrWhiteSpace(_quantityError);
    private bool HasUnitError => !string.IsNullOrWhiteSpace(_unitError);
    private bool HasLowStockThresholdError => !string.IsNullOrWhiteSpace(_lowStockThresholdError);

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

            _categories = await ResourceCategoryService.GetAllResourceCategoriesAsync();
            _foodTypes = await FoodTypeService.GetAllFoodTypesAsync();
            await LoadResourcesAsync();
        }
        catch
        {
            _error = "Resource stock data could not be loaded. Check migrations and database connection.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        ClearResourceValidationErrors();

        if (_form is not null)
        {
            await _form.ValidateAsync();
        }

        var hasCustomValidationErrors = !ValidateResourceForm();
        if ((_form is not null && !_form.IsValid) || hasCustomValidationErrors)
        {
            return;
        }

        _model.ResourceCategoryId = _selectedCategoryId!.Value;
        _model.Quantity = _quantity!.Value;
        _model.LowStockThreshold = _lowStockThreshold!.Value;

        if (!IsFoodCategorySelected)
        {
            _model.FoodTypeId = null;
        }

        _isSaving = true;

        try
        {
            if (_model.Id == 0)
            {
                await ResourceStockService.CreateResourceAsync(_model, _shelterId.Value);
                Snackbar.Add(IsLowStock(_model) ? "Resource saved. Low-stock report sent." : "Resource saved.", Severity.Success);
            }
            else
            {
                await ResourceStockService.UpdateResourceAsync(_model, _shelterId.Value);
                Snackbar.Add(IsLowStock(_model) ? "Resource updated. Low-stock report sent." : "Resource saved.", Severity.Success);
            }

            await ResetFormAsync();
            await LoadResourcesAsync();
        }
        catch (InvalidOperationException ex)
        {
            if (!TryMapResourceValidationMessage(ex.Message))
            {
                _resourceFormError = ex.Message;
            }
        }
        catch
        {
            Snackbar.Add("Could not update resource stock. Please try again.", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ExportResourcesCsvAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        await ExportAsync(() => ExportService.GenerateShelterResourcesCsvAsync(_shelterId.Value));
    }

    private async Task OnResourceCsvSelectedAsync(InputFileChangeEventArgs args)
    {
        _resourceImportFile = args.File;
        await PreviewSelectedResourceFileAsync();
    }

    private async Task PreviewSelectedResourceFileAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        if (_resourceImportFile is null)
        {
            Snackbar.Add("Choose a CSV file first.", Severity.Info);
            return;
        }

        if (!IsCsvFile(_resourceImportFile))
        {
            Snackbar.Add("Please choose a .csv file.", Severity.Warning);
            ClearResourceImportPreview();
            return;
        }

        if (_resourceImportFile.Size > MaxCsvFileSize)
        {
            Snackbar.Add("CSV file is too large. Please use a file under 2 MB.", Severity.Warning);
            ClearResourceImportPreview();
            return;
        }

        _isImporting = true;

        try
        {
            await using var stream = _resourceImportFile.OpenReadStream(MaxCsvFileSize);
            _importResult = await CsvImportService.PreviewShelterResourcesImportAsync(stream, _shelterId.Value);
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

    private async Task ConfirmResourceImportAsync()
    {
        if (_shelterId is null || _resourceImportFile is null)
        {
            Snackbar.Add("Choose a CSV file first.", Severity.Warning);
            return;
        }

        _isImporting = true;

        try
        {
            await using var stream = _resourceImportFile.OpenReadStream(MaxCsvFileSize);
            _importResult = await CsvImportService.ImportShelterResourcesAsync(stream, _shelterId.Value);
            if (_importResult.HasErrors)
            {
                Snackbar.Add("CSV contains validation errors. Please fix them and try again.", Severity.Warning);
                return;
            }

            await LoadResourcesAsync();
            Snackbar.Add("Resources imported successfully.", Severity.Success);
            ClearResourceImportPreview();
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

    private async Task DownloadResourceTemplateAsync()
    {
        await FileDownloadService.DownloadAsync(CsvImportService.GenerateShelterResourcesTemplate());
    }

    private void ClearResourceImportPreview()
    {
        _importResult = null;
        _resourceImportFile = null;
        _resourceImportInputKey++;
    }

    private async Task ExportResourcesPdfAsync()
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        await ExportAsync(() => ExportService.GenerateShelterResourcesPdfAsync(_shelterId.Value));
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

    private void EditResource(ResourceStock resource)
    {
        ClearResourceValidationErrors();
        _model = new ResourceStock
        {
            Id = resource.Id,
            Name = resource.Name,
            ResourceCategoryId = resource.ResourceCategoryId,
            FoodTypeId = resource.FoodTypeId,
            Quantity = resource.Quantity,
            Unit = resource.Unit,
            LowStockThreshold = resource.LowStockThreshold
        };
        _selectedCategoryId = resource.ResourceCategoryId;
        _quantity = resource.Quantity;
        _lowStockThreshold = resource.LowStockThreshold;
    }

    private async Task ConfirmDeleteAsync(ResourceStock resource)
    {
        if (!await ConfirmAsync(
            "Delete resource item",
            $"Are you sure you want to delete {resource.Name}? This action cannot easily be undone.",
            "Delete",
            Color.Error,
            Icons.Material.Filled.DeleteForever))
        {
            return;
        }

        await DeleteAsync(resource);
    }

    private async Task DeleteAsync(ResourceStock resource)
    {
        if (_shelterId is null)
        {
            Snackbar.Add("Current shelter profile could not be found.", Severity.Error);
            return;
        }

        _isSaving = true;

        try
        {
            await ResourceStockService.DeleteResourceAsync(resource.Id, _shelterId.Value);
            _resources.Remove(resource);
            Snackbar.Add("Resource deleted.", Severity.Success);
        }
        catch (InvalidOperationException ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
        }
        catch
        {
            Snackbar.Add("Could not delete resource stock. Please try again.", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void OnCategoryChanged(int? categoryId)
    {
        _selectedCategoryId = categoryId;
        _model.ResourceCategoryId = categoryId ?? 0;
        _resourceCategoryError = null;
        _resourceNameError = null;
        _resourceFormError = null;
        if (!IsFoodCategorySelected)
        {
            _model.FoodTypeId = null;
            _foodTypeError = null;
        }
    }

    private async Task ResetFormAsync()
    {
        _model = new ResourceStock();
        _selectedCategoryId = null;
        _quantity = null;
        _lowStockThreshold = null;
        ClearResourceValidationErrors();
        if (_form is not null)
        {
            await _form.ResetValidationAsync();
        }
        _form = null;
        _resourceFormKey++;
    }

    private void ClearResourceValidationErrors()
    {
        _resourceFormError = null;
        _resourceNameError = null;
        _resourceCategoryError = null;
        _foodTypeError = null;
        _quantityError = null;
        _unitError = null;
        _lowStockThresholdError = null;
    }

    private bool ValidateResourceForm()
    {
        var isValid = true;
        if (string.IsNullOrWhiteSpace(_model.Name))
        {
            _resourceNameError = "Name is required.";
            isValid = false;
        }

        if (!_selectedCategoryId.HasValue || _selectedCategoryId.Value <= 0)
        {
            _resourceCategoryError = "Category is required.";
            isValid = false;
        }

        if (!_quantity.HasValue)
        {
            _quantityError = "Quantity is required.";
            isValid = false;
        }
        else if (_quantity.Value <= 0)
        {
            _quantityError = "Quantity must be greater than zero.";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(_model.Unit))
        {
            _unitError = "Unit is required.";
            isValid = false;
        }

        if (!_lowStockThreshold.HasValue)
        {
            _lowStockThresholdError = "Low-stock threshold is required.";
            isValid = false;
        }
        else if (_lowStockThreshold.Value < 0)
        {
            _lowStockThresholdError = "Low-stock threshold must be zero or greater.";
            isValid = false;
        }

        return isValid;
    }

    private void ClearResourceNameError()
    {
        _resourceNameError = null;
        _resourceFormError = null;
    }

    private void ClearFoodTypeError()
    {
        _foodTypeError = null;
        _resourceFormError = null;
    }

    private void ClearQuantityError()
    {
        _quantityError = null;
        _resourceFormError = null;
    }

    private void ClearUnitError()
    {
        _unitError = null;
        _resourceFormError = null;
    }

    private void ClearLowStockThresholdError()
    {
        _lowStockThresholdError = null;
        _resourceFormError = null;
    }

    private bool TryMapResourceValidationMessage(string message)
    {
        switch (message)
        {
            case "Resource name is required.":
                _resourceNameError = message;
                return true;
            case "Resource category is required.":
                _resourceCategoryError = message;
                return true;
            case "Food type is required for food resources.":
            case "Selected food type was not found.":
                _foodTypeError = message;
                return true;
            case "Quantity must be greater than zero.":
                _quantityError = message;
                return true;
            case "Low-stock threshold must be zero or greater.":
                _lowStockThresholdError = message;
                return true;
            case "Unit is required.":
                _unitError = message;
                return true;
            case "This resource already exists in your shelter stock.":
                _resourceNameError = message;
                return true;
            default:
                return false;
        }
    }

    private async Task LoadResourcesAsync()
    {
        if (_shelterId.HasValue)
        {
            _resources = await ResourceStockService.GetResourcesForShelterAsync(_shelterId.Value);
        }
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

    private static bool IsLowStock(ResourceStock resource)
    {
        return resource.Quantity <= resource.LowStockThreshold;
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
