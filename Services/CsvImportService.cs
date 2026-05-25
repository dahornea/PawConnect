using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class CsvImportService(
    ApplicationDbContext context,
    IAuditLogService? auditLogService = null,
    INotificationService? notificationService = null) : ICsvImportService
{
    private const string CsvContentType = "text/csv;charset=utf-8";
    private static readonly Encoding CsvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private static readonly string[] ResourceColumns =
    [
        "Name",
        "Category",
        "FoodType",
        "Quantity",
        "Unit",
        "LowStockThreshold"
    ];

    private static readonly string[] DogColumns =
    [
        "Name",
        "Breed",
        "AgeYears",
        "AgeMonths",
        "Size",
        "Status",
        "Location",
        "Description",
        "PreferredFoodType",
        "DailyFoodAmount",
        "ImageUrls"
    ];

    private static readonly string[] ShelterRequestColumns =
    [
        "ShelterName",
        "ContactPersonName",
        "Email",
        "PhoneNumber",
        "City",
        "Address",
        "Description",
        "Website",
        "OpeningHours",
        "ReasonForJoining",
        "Latitude",
        "Longitude"
    ];

    public Task<CsvImportResult> PreviewShelterResourcesImportAsync(Stream csvStream, int shelterId)
    {
        return BuildResourceImportPreviewAsync(csvStream, shelterId);
    }

    public async Task<CsvImportResult> ImportShelterResourcesAsync(Stream csvStream, int shelterId)
    {
        var preview = await BuildResourceImportPreviewAsync(csvStream, shelterId);
        if (preview.HasErrors || preview.ValidRows == 0)
        {
            return preview;
        }

        var categories = await GetResourceCategoriesAsync();
        var foodTypes = await GetFoodTypesAsync();
        var resources = await context.ResourceStocks
            .Where(resource => resource.ShelterId == shelterId)
            .ToListAsync();

        foreach (var row in preview.RowResults)
        {
            var name = row.PreviewData["Name"]!;
            var category = FindCategory(categories, row.PreviewData["Category"]!)!;
            var isFood = IsFoodCategory(category);
            var foodType = isFood ? FindFoodType(foodTypes, row.PreviewData["FoodType"]) : null;
            var quantity = int.Parse(row.PreviewData["Quantity"]!, CultureInfo.InvariantCulture);
            var threshold = int.Parse(row.PreviewData["LowStockThreshold"]!, CultureInfo.InvariantCulture);
            var unit = row.PreviewData["Unit"]!;
            var key = BuildResourceKey(name, category.Id, isFood ? foodType?.Id : null);
            var existing = resources.FirstOrDefault(resource => BuildResourceKey(resource.Name, resource.ResourceCategoryId, resource.FoodTypeId) == key);

            if (existing is null)
            {
                existing = new ResourceStock
                {
                    ShelterId = shelterId,
                    Name = name,
                    ResourceCategoryId = category.Id,
                    FoodTypeId = isFood ? foodType!.Id : null,
                    Quantity = quantity,
                    Unit = unit,
                    LowStockThreshold = threshold,
                    LastUpdatedAt = DateTime.UtcNow
                };
                context.ResourceStocks.Add(existing);
                resources.Add(existing);
            }
            else
            {
                existing.Name = name;
                existing.ResourceCategoryId = category.Id;
                existing.FoodTypeId = isFood ? foodType!.Id : null;
                existing.Quantity = quantity;
                existing.Unit = unit;
                existing.LowStockThreshold = threshold;
                existing.LastUpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
        preview.ImportedRows = preview.ValidRows;
        await LogImportAsync(AuditActions.ResourceCsvImported, "ResourceStock", shelterId, preview.ImportedRows);
        return preview;
    }

    public Task<CsvImportResult> PreviewShelterDogsImportAsync(Stream csvStream, int shelterId)
    {
        return BuildDogImportPreviewAsync(csvStream, shelterId);
    }

    public async Task<CsvImportResult> ImportShelterDogsAsync(Stream csvStream, int shelterId)
    {
        var preview = await BuildDogImportPreviewAsync(csvStream, shelterId);
        if (preview.HasErrors || preview.ValidRows == 0)
        {
            return preview;
        }

        var foodTypes = await GetFoodTypesAsync();
        var dogBreeds = await context.DogBreeds.AsNoTracking().ToListAsync();
        foreach (var row in preview.RowResults)
        {
            var preferredFoodType = FindFoodType(foodTypes, row.PreviewData["PreferredFoodType"]);
            var parsedBreed = DogBreedFormatter.Parse(row.PreviewData["Breed"], dogBreeds);
            var dog = new Dog
            {
                Name = row.PreviewData["Name"]!,
                Breed = parsedBreed.DisplayName,
                DogBreedId = parsedBreed.DogBreedId,
                SecondaryBreedId = parsedBreed.SecondaryBreedId,
                IsMixedBreed = parsedBreed.IsMixedBreed,
                CustomBreedName = parsedBreed.CustomBreedName,
                AgeYears = int.Parse(row.PreviewData["AgeYears"]!, CultureInfo.InvariantCulture),
                AgeMonths = int.Parse(row.PreviewData["AgeMonths"]!, CultureInfo.InvariantCulture),
                Age = int.Parse(row.PreviewData["AgeYears"]!, CultureInfo.InvariantCulture),
                Size = Enum.Parse<DogSize>(row.PreviewData["Size"]!, ignoreCase: true),
                Status = Enum.Parse<DogStatus>(row.PreviewData["Status"]!, ignoreCase: true),
                Location = row.PreviewData["Location"]!,
                Description = NormalizeOptional(row.PreviewData["Description"]),
                PreferredFoodTypeId = preferredFoodType?.Id,
                DailyFoodAmountGrams = ParseOptionalInt(row.PreviewData["DailyFoodAmount"]),
                ShelterId = shelterId
            };

            var imageUrls = SplitImageUrls(row.PreviewData["ImageUrls"]);
            foreach (var imageUrl in imageUrls)
            {
                if (!DogImageUrlValidator.TryNormalize(imageUrl, out var normalizedImageUrl))
                {
                    continue;
                }

                dog.Images.Add(new DogImage
                {
                    ImageUrl = normalizedImageUrl,
                    IsMainImage = dog.Images.Count == 0
                });
            }

            context.Dogs.Add(dog);
        }

        await context.SaveChangesAsync();
        preview.ImportedRows = preview.ValidRows;
        await LogImportAsync(AuditActions.DogCsvImported, "Dog", shelterId, preview.ImportedRows);
        return preview;
    }

    public Task<CsvImportResult> PreviewAdminShelterRequestsImportAsync(Stream csvStream)
    {
        return BuildShelterRequestImportPreviewAsync(csvStream);
    }

    public async Task<CsvImportResult> ImportAdminShelterRequestsAsync(Stream csvStream)
    {
        var preview = await BuildShelterRequestImportPreviewAsync(csvStream);
        if (preview.HasErrors || preview.ValidRows == 0)
        {
            return preview;
        }

        foreach (var row in preview.RowResults)
        {
            context.ShelterRegistrationRequests.Add(new ShelterRegistrationRequest
            {
                ShelterName = row.PreviewData["ShelterName"]!,
                ContactPersonName = row.PreviewData["ContactPersonName"]!,
                Email = row.PreviewData["Email"]!,
                PhoneNumber = row.PreviewData["PhoneNumber"]!,
                City = row.PreviewData["City"]!,
                Address = row.PreviewData["Address"]!,
                Description = row.PreviewData["Description"]!,
                Website = NormalizeOptional(row.PreviewData["Website"]),
                OpeningHours = NormalizeOptional(row.PreviewData["OpeningHours"]),
                ReasonForJoining = NormalizeOptional(row.PreviewData["ReasonForJoining"]),
                Latitude = ParseOptionalDouble(row.PreviewData["Latitude"]),
                Longitude = ParseOptionalDouble(row.PreviewData["Longitude"]),
                Status = ShelterRegistrationRequestStatus.Pending,
                SubmittedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
        preview.ImportedRows = preview.ValidRows;
        await LogAdminShelterRequestsImportAsync(preview.ImportedRows);
        await NotifyAdminsOfShelterRequestImportAsync(preview.ImportedRows);
        return preview;
    }

    public ExportFile GenerateShelterResourcesTemplate()
    {
        return BuildTemplate(
            "pawconnect-resource-import-template.csv",
            ResourceColumns,
            ["Adult dry food", "Food", "Adult dry food", "25", "kg", "10"],
            ["Blankets", "Blankets", "", "12", "pieces", "5"],
            ["Cleaning spray", "Cleaning Supplies", "", "8", "bottles", "3"]);
    }

    public ExportFile GenerateShelterDogsTemplate()
    {
        return BuildTemplate(
            "pawconnect-dog-import-template.csv",
            DogColumns,
            ["Buddy", "Labrador Mix", "2", "6", "Large", "Available", "Cluj-Napoca", "Friendly dog", "Adult dry food", "480", "https://example.com/dog1.jpg;https://example.com/dog2.jpg"]);
    }

    public ExportFile GenerateAdminShelterRequestsTemplate()
    {
        return BuildTemplate(
            "pawconnect-shelter-requests-import-template.csv",
            ShelterRequestColumns,
            [
                "Happy Tails Rescue",
                "Alex Popescu",
                "happytails@example.com",
                "+40 700 000 100",
                "Cluj-Napoca",
                "Strada Exemplu 10",
                "Fictional demo shelter for PawConnect testing",
                "https://example.com",
                "Mon-Fri 09:00-17:00",
                "We want to list adoptable dogs",
                "46.7712",
                "23.6236"
            ]);
    }

    private async Task<CsvImportResult> BuildResourceImportPreviewAsync(Stream csvStream, int shelterId)
    {
        var parsed = await ParseCsvAsync(csvStream, ResourceColumns);
        if (parsed.HeaderErrors.Count > 0)
        {
            return BuildHeaderErrorResult(parsed.HeaderErrors);
        }

        var categories = await GetResourceCategoriesAsync();
        var foodTypes = await GetFoodTypesAsync();
        var existingResources = await context.ResourceStocks
            .Where(resource => resource.ShelterId == shelterId)
            .AsNoTracking()
            .ToListAsync();

        var result = new CsvImportResult { TotalRows = parsed.Rows.Count };
        var csvKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parsedRow in parsed.Rows)
        {
            var row = CreateRowResult(parsedRow.RowNumber, ResourceColumns, parsedRow.Values);
            ValidateResourceRow(row, categories, foodTypes, existingResources, csvKeys);
            result.RowResults.Add(row);
        }

        return result;
    }

    private async Task<CsvImportResult> BuildDogImportPreviewAsync(Stream csvStream, int shelterId)
    {
        var parsed = await ParseCsvAsync(csvStream, DogColumns);
        if (parsed.HeaderErrors.Count > 0)
        {
            return BuildHeaderErrorResult(parsed.HeaderErrors);
        }

        var foodTypes = await GetFoodTypesAsync();
        var result = new CsvImportResult { TotalRows = parsed.Rows.Count };
        var csvKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parsedRow in parsed.Rows)
        {
            var row = CreateRowResult(parsedRow.RowNumber, DogColumns, parsedRow.Values);
            ValidateDogRow(row, foodTypes, csvKeys);
            result.RowResults.Add(row);
        }

        return result;
    }

    private async Task<CsvImportResult> BuildShelterRequestImportPreviewAsync(Stream csvStream)
    {
        var parsed = await ParseCsvAsync(csvStream, ShelterRequestColumns);
        if (parsed.HeaderErrors.Count > 0)
        {
            return BuildHeaderErrorResult(parsed.HeaderErrors);
        }

        var existingPendingEmails = await context.ShelterRegistrationRequests
            .Where(request => request.Status == ShelterRegistrationRequestStatus.Pending)
            .Select(request => request.Email)
            .AsNoTracking()
            .ToListAsync();
        var existingShelterEmails = await context.Shelters
            .Where(shelter => shelter.Email != null)
            .Select(shelter => shelter.Email!)
            .AsNoTracking()
            .ToListAsync();
        var existingUserEmails = await context.Users
            .Where(user => user.Email != null)
            .Select(user => user.Email!)
            .AsNoTracking()
            .ToListAsync();

        var result = new CsvImportResult { TotalRows = parsed.Rows.Count };
        var csvEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parsedRow in parsed.Rows)
        {
            var row = CreateRowResult(parsedRow.RowNumber, ShelterRequestColumns, parsedRow.Values);
            ValidateShelterRequestRow(row, existingPendingEmails, existingShelterEmails, existingUserEmails, csvEmails);
            result.RowResults.Add(row);
        }

        return result;
    }

    private static void ValidateResourceRow(
        CsvImportRowResult row,
        IReadOnlyList<ResourceCategory> categories,
        IReadOnlyList<FoodType> foodTypes,
        IReadOnlyList<ResourceStock> existingResources,
        HashSet<string> csvKeys)
    {
        var name = Require(row, "Name", "Name is required.");
        var categoryValue = Require(row, "Category", "Category is required.");
        var unit = Require(row, "Unit", "Unit is required.");
        var quantity = ParseRequiredNonNegativeInt(row, "Quantity", "Quantity must be zero or greater.");
        var threshold = ParseRequiredNonNegativeInt(row, "LowStockThreshold", "Low-stock threshold must be zero or greater.");

        ResourceCategory? category = null;
        FoodType? foodType = null;

        if (!string.IsNullOrWhiteSpace(categoryValue))
        {
            category = FindCategory(categories, categoryValue);
            if (category is null)
            {
                AddError(row, "Category", $"Category '{categoryValue}' was not found.");
            }
        }

        if (category is not null && IsFoodCategory(category))
        {
            var foodTypeValue = Require(row, "FoodType", "Food type is required for food resources.");
            if (!string.IsNullOrWhiteSpace(foodTypeValue))
            {
                foodType = FindFoodType(foodTypes, foodTypeValue);
                if (foodType is null)
                {
                    AddError(row, "FoodType", $"Food type '{foodTypeValue}' was not found.");
                }
            }
        }
        else
        {
            row.PreviewData["FoodType"] = null;
        }

        if (category is null || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit) || quantity is null || threshold is null)
        {
            return;
        }

        var key = BuildResourceKey(name, category.Id, IsFoodCategory(category) ? foodType?.Id : null);
        if (!csvKeys.Add(key))
        {
            AddError(row, "Name", "Duplicate resource row in this CSV.");
        }

        row.Action = existingResources.Any(resource => BuildResourceKey(resource.Name, resource.ResourceCategoryId, resource.FoodTypeId) == key)
            ? CsvImportActions.Update
            : CsvImportActions.Create;
    }

    private static void ValidateDogRow(CsvImportRowResult row, IReadOnlyList<FoodType> foodTypes, HashSet<string> csvKeys)
    {
        var name = Require(row, "Name", "Dog name is required.");
        var breed = Require(row, "Breed", "Breed is required.");
        var location = Require(row, "Location", "Location is required.");
        var ageYears = ParseRequiredNonNegativeInt(row, "AgeYears", "Age in years must be zero or greater.");
        var ageMonths = ParseRequiredNonNegativeInt(row, "AgeMonths", "Age in months must be between 0 and 11.");
        var dailyFoodAmount = ParseOptionalNonNegativeInt(row, "DailyFoodAmount", "Daily food amount must be zero or greater.");

        if (ageMonths is > 11)
        {
            AddError(row, "AgeMonths", "Age in months must be between 0 and 11.");
        }

        if (ageYears == 0 && ageMonths == 0)
        {
            AddError(row, "AgeYears", "Please enter the dog's age in years or months.");
        }

        if (!Enum.TryParse<DogSize>(row.PreviewData["Size"], true, out var size))
        {
            AddError(row, "Size", "Size must be Small, Medium, Large, or ExtraLarge.");
        }
        else
        {
            row.PreviewData["Size"] = size.ToString();
        }

        if (!Enum.TryParse<DogStatus>(row.PreviewData["Status"], true, out var status))
        {
            AddError(row, "Status", "Status must be Available, Reserved, Adopted, or InTreatment.");
        }
        else
        {
            row.PreviewData["Status"] = status.ToString();
        }

        var preferredFoodType = NormalizeOptional(row.PreviewData["PreferredFoodType"]);
        if (!string.IsNullOrWhiteSpace(preferredFoodType) && FindFoodType(foodTypes, preferredFoodType) is null)
        {
            AddError(row, "PreferredFoodType", $"Food type '{preferredFoodType}' was not found.");
        }

        var imageUrls = SplitImageUrls(row.PreviewData["ImageUrls"]);
        var normalizedImageUrls = new List<string>();
        var uniqueImageUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var imageUrl in imageUrls)
        {
            if (!DogImageUrlValidator.TryNormalize(imageUrl, out var normalizedImageUrl))
            {
                AddError(row, "ImageUrls", $"Image URL '{imageUrl}' must be a valid direct image URL starting with http:// or https://.");
                continue;
            }

            if (!uniqueImageUrls.Add(normalizedImageUrl))
            {
                AddError(row, "ImageUrls", "Duplicate image URL in this row.");
                continue;
            }

            normalizedImageUrls.Add(normalizedImageUrl);
        }

        row.PreviewData["ImageUrls"] = string.Join(";", normalizedImageUrls);

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(breed) ||
            string.IsNullOrWhiteSpace(location) ||
            ageYears is null ||
            ageMonths is null ||
            dailyFoodAmount is < 0)
        {
            return;
        }

        var key = string.Join("|", NormalizeKey(name), NormalizeKey(breed), ageYears, ageMonths, NormalizeKey(location));
        if (!csvKeys.Add(key))
        {
            AddError(row, "Name", "Duplicate dog row in this CSV.");
        }
    }

    private static void ValidateShelterRequestRow(
        CsvImportRowResult row,
        IReadOnlyList<string> existingPendingEmails,
        IReadOnlyList<string> existingShelterEmails,
        IReadOnlyList<string> existingUserEmails,
        HashSet<string> csvEmails)
    {
        var shelterName = RequireAndLimit(row, "ShelterName", "Shelter name is required.", 120);
        var contactPersonName = RequireAndLimit(row, "ContactPersonName", "Contact person name is required.", 120);
        var email = RequireAndLimit(row, "Email", "Email is required.", 120);
        var phoneNumber = RequireAndLimit(row, "PhoneNumber", "Phone number is required.", 30);
        var city = RequireAndLimit(row, "City", "City is required.", 80);
        var address = RequireAndLimit(row, "Address", "Address is required.", 160);
        RequireAndLimit(row, "Description", "Description is required.", 1000);
        LimitOptional(row, "Website", 200);
        LimitOptional(row, "OpeningHours", 200);
        LimitOptional(row, "ReasonForJoining", 1000);

        if (!string.IsNullOrWhiteSpace(email) && !new EmailAddressAttribute().IsValid(email))
        {
            AddError(row, "Email", "Email must be valid.");
        }

        if (!string.IsNullOrWhiteSpace(row.PreviewData["Website"]) &&
            !new UrlAttribute().IsValid(row.PreviewData["Website"]))
        {
            AddError(row, "Website", "Website must be a valid URL.");
        }

        var latitude = ParseOptionalDouble(row, "Latitude", "Latitude must be between -90 and 90.");
        if (latitude is < -90 or > 90)
        {
            AddError(row, "Latitude", "Latitude must be between -90 and 90.");
        }

        var longitude = ParseOptionalDouble(row, "Longitude", "Longitude must be between -180 and 180.");
        if (longitude is < -180 or > 180)
        {
            AddError(row, "Longitude", "Longitude must be between -180 and 180.");
        }

        if (!string.IsNullOrWhiteSpace(address) && !string.IsNullOrWhiteSpace(city))
        {
            row.PreviewData["Address"] = NormalizeAddressWithoutCity(address, city);
        }

        var normalizedEmail = NormalizeKey(email);
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            if (!csvEmails.Add(normalizedEmail))
            {
                row.Action = CsvImportActions.Duplicate;
                AddError(row, "Email", "Duplicate shelter email in this CSV.");
            }

            if (existingPendingEmails.Any(existingEmail => NormalizeKey(existingEmail) == normalizedEmail))
            {
                row.Action = CsvImportActions.Duplicate;
                AddError(row, "Email", "A pending shelter application already exists for this email.");
            }

            if (existingShelterEmails.Any(existingEmail => NormalizeKey(existingEmail) == normalizedEmail) ||
                existingUserEmails.Any(existingEmail => NormalizeKey(existingEmail) == normalizedEmail))
            {
                row.Action = CsvImportActions.Duplicate;
                AddError(row, "Email", "A shelter account with this email already exists.");
            }
        }

        if (row.IsValid)
        {
            row.Action = CsvImportActions.CreatePendingRequest;
        }
        else if (row.Action != CsvImportActions.Duplicate)
        {
            row.Action = CsvImportActions.Skip;
        }
    }

    private async Task<List<ResourceCategory>> GetResourceCategoriesAsync()
    {
        return await context.ResourceCategories.AsNoTracking().ToListAsync();
    }

    private async Task<List<FoodType>> GetFoodTypesAsync()
    {
        return await context.FoodTypes.AsNoTracking().ToListAsync();
    }

    private Task LogImportAsync(string action, string entityName, int shelterId, int importedRows)
    {
        return auditLogService?.LogAsync(
            action,
            entityName,
            shelterId.ToString(CultureInfo.InvariantCulture),
            $"{importedRows} row(s) were imported from CSV.",
            additionalData: $"ShelterId={shelterId};ImportedRows={importedRows}") ?? Task.CompletedTask;
    }

    private Task LogAdminShelterRequestsImportAsync(int importedRows)
    {
        return auditLogService?.LogAsync(
            AuditActions.ShelterRequestsCsvImported,
            "ShelterRegistrationRequest",
            null,
            $"{importedRows} pending shelter request row(s) were imported from CSV.",
            additionalData: $"ImportedRows={importedRows}") ?? Task.CompletedTask;
    }

    private async Task NotifyAdminsOfShelterRequestImportAsync(int importedRows)
    {
        if (notificationService is null || importedRows <= 0)
        {
            return;
        }

        try
        {
            var adminRoleId = await context.Roles
                .Where(role => role.NormalizedName == IdentitySeedData.AdminRole.ToUpperInvariant())
                .Select(role => role.Id)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(adminRoleId))
            {
                return;
            }

            var adminUserIds = await context.UserRoles
                .Where(userRole => userRole.RoleId == adminRoleId)
                .Select(userRole => userRole.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var adminUserId in adminUserIds)
            {
                await notificationService.CreateNotificationAsync(
                    adminUserId,
                    "Shelter requests imported",
                    $"{importedRows} shelter application request(s) were imported from CSV.",
                    NotificationCategory.ShelterApplication,
                    NotificationType.Info,
                    "/admin/shelter-requests",
                    "ShelterRegistrationRequest");
            }
        }
        catch
        {
            // Import success should not depend on in-app notification delivery.
        }
    }

    private static CsvImportRowResult CreateRowResult(int rowNumber, IReadOnlyList<string> columns, IReadOnlyList<string> values)
    {
        var row = new CsvImportRowResult { RowNumber = rowNumber };
        for (var index = 0; index < columns.Count; index++)
        {
            row.PreviewData[columns[index]] = index < values.Count ? NormalizeOptional(values[index]) : null;
        }

        return row;
    }

    private static string? Require(CsvImportRowResult row, string field, string message)
    {
        var value = NormalizeOptional(row.PreviewData[field]);
        row.PreviewData[field] = value;
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(row, field, message);
        }

        return value;
    }

    private static string? RequireAndLimit(CsvImportRowResult row, string field, string requiredMessage, int maxLength)
    {
        var value = Require(row, field, requiredMessage);
        if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
        {
            AddError(row, field, $"{field} must be {maxLength} characters or fewer.");
        }

        return value;
    }

    private static void LimitOptional(CsvImportRowResult row, string field, int maxLength)
    {
        var value = NormalizeOptional(row.PreviewData[field]);
        row.PreviewData[field] = value;
        if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
        {
            AddError(row, field, $"{field} must be {maxLength} characters or fewer.");
        }
    }

    private static int? ParseRequiredNonNegativeInt(CsvImportRowResult row, string field, string message)
    {
        var value = NormalizeOptional(row.PreviewData[field]);
        row.PreviewData[field] = value;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            AddError(row, field, message);
            return null;
        }

        row.PreviewData[field] = parsed.ToString(CultureInfo.InvariantCulture);
        return parsed;
    }

    private static int? ParseOptionalNonNegativeInt(CsvImportRowResult row, string field, string message)
    {
        var value = NormalizeOptional(row.PreviewData[field]);
        row.PreviewData[field] = value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            AddError(row, field, message);
            return null;
        }

        row.PreviewData[field] = parsed.ToString(CultureInfo.InvariantCulture);
        return parsed;
    }

    private static int? ParseOptionalInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static double? ParseOptionalDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static double? ParseOptionalDouble(CsvImportRowResult row, string field, string message)
    {
        var value = NormalizeOptional(row.PreviewData[field]);
        row.PreviewData[field] = value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            AddError(row, field, message);
            return null;
        }

        row.PreviewData[field] = parsed.ToString("0.######", CultureInfo.InvariantCulture);
        return parsed;
    }

    private static void AddError(CsvImportRowResult row, string field, string message)
    {
        if (row.Errors.Any(error => error.Field == field && error.Message == message))
        {
            return;
        }

        row.Errors.Add(new CsvImportValidationError { Field = field, Message = message });
    }

    private static ResourceCategory? FindCategory(IReadOnlyList<ResourceCategory> categories, string? value)
    {
        var normalized = NormalizeKey(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var exact = categories.FirstOrDefault(category => NormalizeKey(category.Name) == normalized);
        if (exact is not null)
        {
            return exact;
        }

        return normalized == "cleaning"
            ? categories.FirstOrDefault(category => category.Name.Equals("Cleaning Supplies", StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static FoodType? FindFoodType(IReadOnlyList<FoodType> foodTypes, string? value)
    {
        var normalized = NormalizeKey(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : foodTypes.FirstOrDefault(foodType => NormalizeKey(foodType.Name) == normalized);
    }

    private static bool IsFoodCategory(ResourceCategory category)
    {
        return category.Name.Equals("Food", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildResourceKey(string name, int categoryId, int? foodTypeId)
    {
        return string.Join("|", NormalizeKey(name), categoryId.ToString(CultureInfo.InvariantCulture), foodTypeId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static IReadOnlyList<string> SplitImageUrls(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(imageUrl => !string.IsNullOrWhiteSpace(imageUrl))
            .ToList();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    }

    private static string NormalizeAddressWithoutCity(string address, string city)
    {
        var normalizedAddress = address.Trim();
        var normalizedCity = city.Trim();

        if (string.IsNullOrWhiteSpace(normalizedCity))
        {
            return normalizedAddress;
        }

        var citySuffix = $", {normalizedCity}";
        return normalizedAddress.EndsWith(citySuffix, StringComparison.OrdinalIgnoreCase)
            ? normalizedAddress[..^citySuffix.Length].Trim()
            : normalizedAddress;
    }

    private static CsvImportResult BuildHeaderErrorResult(IReadOnlyList<CsvImportValidationError> errors)
    {
        return new CsvImportResult
        {
            RowResults =
            [
                new CsvImportRowResult
                {
                    RowNumber = 1,
                    Action = CsvImportActions.Skip,
                    Errors = errors.ToList()
                }
            ]
        };
    }

    private static async Task<ParsedCsv> ParseCsvAsync(Stream stream, IReadOnlyList<string> requiredHeaders)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync();
        var records = ParseCsvRecords(text)
            .Where(record => record.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToList();

        if (records.Count == 0)
        {
            return new ParsedCsv([], [], [new CsvImportValidationError { Field = "File", Message = "CSV file is empty." }]);
        }

        var header = records[0].Select(value => value.Trim()).ToList();
        var headerErrors = requiredHeaders
            .Where(requiredHeader => !header.Any(actual => actual.Equals(requiredHeader, StringComparison.OrdinalIgnoreCase)))
            .Select(requiredHeader => new CsvImportValidationError { Field = requiredHeader, Message = $"Missing required column '{requiredHeader}'." })
            .ToList();

        if (headerErrors.Count > 0)
        {
            return new ParsedCsv(header, [], headerErrors);
        }

        var rows = new List<ParsedCsvRow>();
        for (var index = 1; index < records.Count; index++)
        {
            var record = records[index];
            var values = requiredHeaders.Select(requiredHeader =>
            {
                var columnIndex = header.FindIndex(actual => actual.Equals(requiredHeader, StringComparison.OrdinalIgnoreCase));
                return columnIndex >= 0 && columnIndex < record.Count ? record[columnIndex] : string.Empty;
            }).ToList();

            rows.Add(new ParsedCsvRow(index + 1, values));
        }

        return new ParsedCsv(header, rows, []);
    }

    private static List<List<string>> ParseCsvRecords(string text)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (current == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (current == ',' && !inQuotes)
            {
                row.Add(value.ToString());
                value.Clear();
                continue;
            }

            if ((current == '\r' || current == '\n') && !inQuotes)
            {
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(value.ToString());
                records.Add(row);
                row = [];
                value.Clear();
                continue;
            }

            value.Append(current);
        }

        row.Add(value.ToString());
        records.Add(row);
        return records;
    }

    private static ExportFile BuildTemplate(string fileName, IReadOnlyList<string> headers, params IReadOnlyList<string>[] rows)
    {
        var builder = new StringBuilder();
        AppendCsvRow(builder, headers);
        foreach (var row in rows)
        {
            AppendCsvRow(builder, row);
        }

        return new ExportFile(fileName, CsvContentType, CsvEncoding.GetBytes(builder.ToString()));
    }

    private static void AppendCsvRow(StringBuilder builder, IReadOnlyList<string?> values)
    {
        builder.AppendLine(string.Join(",", values.Select(EscapeCsvValue)));
    }

    private static string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        if (normalized.Contains('"') || normalized.Contains(',') || normalized.Contains('\n') || normalized.Contains('\r') || normalized.Contains(';'))
        {
            return $"\"{normalized.Replace("\"", "\"\"")}\"";
        }

        return normalized;
    }

    private sealed record ParsedCsv(IReadOnlyList<string> Headers, IReadOnlyList<ParsedCsvRow> Rows, IReadOnlyList<CsvImportValidationError> HeaderErrors);

    private sealed record ParsedCsvRow(int RowNumber, IReadOnlyList<string> Values);
}
