using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PawConnect.Services;

public class ExportService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    ILogger<ExportService> logger,
    IAuditLogService? auditLogService = null,
    IReportHistoryService? reportHistoryService = null) : IExportService
{
    private const string CsvContentType = "text/csv;charset=utf-8";
    private const string PdfContentType = "application/pdf";
    private static readonly Encoding CsvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    static ExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ExportFile> GenerateUsersCsvAsync()
    {
        var users = await userManager.Users
            .OrderBy(u => u.Email)
            .AsNoTracking()
            .ToListAsync();

        var rows = new List<IReadOnlyList<string?>>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            rows.Add([
                user.Id,
                user.Email,
                user.UserName,
                user.FullName,
                user.PhoneNumber,
                string.Join("; ", roles),
                FormatYesNo(user.EmailConfirmed)
            ]);
        }

        var file = BuildCsv(
            "pawconnect-users",
            ["User Id", "Email", "UserName", "Full Name", "PhoneNumber", "Roles", "EmailConfirmed"],
            rows);
        await LogExportAsync(file, "Admin users CSV export was generated.", relatedEntityName: "Users");
        return file;
    }

    public async Task<ExportFile> GenerateSheltersCsvAsync()
    {
        var shelters = await context.Shelters
            .Include(s => s.Dogs)
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync();

        var rows = shelters.Select(s => new[]
        {
            s.Id.ToString(CultureInfo.InvariantCulture),
            s.Name,
            s.Email,
            s.PhoneNumber,
            s.City,
            s.Address,
            s.Description,
            FormatCoordinate(s.Latitude),
            FormatCoordinate(s.Longitude),
            s.Dogs.Count.ToString(CultureInfo.InvariantCulture)
        });

        var file = BuildCsv(
            "pawconnect-shelters",
            ["Shelter Id", "Shelter Name", "Email", "Phone Number", "City", "Address", "Description", "Latitude", "Longitude", "Number of Dogs"],
            rows);
        await LogExportAsync(file, "Admin shelters CSV export was generated.", relatedEntityName: "Shelters");
        return file;
    }

    public async Task<ExportFile> GenerateDogsCsvAsync()
    {
        var dogs = await context.Dogs
            .Include(d => d.Shelter)
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
            .Include(d => d.PreferredFoodType)
            .OrderBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync();

        var rows = dogs.Select(d => new[]
        {
            d.Id.ToString(CultureInfo.InvariantCulture),
            d.Name,
            DogBreedFormatter.Format(d),
            DogAgeFormatter.Format(d),
            d.Size.ToString(),
            d.Location,
            d.Shelter?.Name,
            d.Status.ToString(),
            d.PreferredFoodType?.Name,
            d.DailyFoodAmountGrams?.ToString(CultureInfo.InvariantCulture),
            HasSuccessStory(d) ? "Has story" : "No story",
            FormatDate(d.AdoptedAt)
        });

        var file = BuildCsv(
            "pawconnect-dogs",
            ["Dog Id", "Name", "Breed", "Age", "Size", "Location", "Shelter Name", "Status", "Preferred Food Type", "Daily Food Amount Grams", "Success Story", "AdoptedAt"],
            rows);
        await LogExportAsync(file, "Admin dogs CSV export was generated.", relatedEntityName: "Dogs");
        return file;
    }

    public async Task<ExportFile> GenerateAdoptionRequestsCsvAsync()
    {
        var requests = await GetAdoptionRequestsAsync();
        var rows = requests.Select(r => new[]
        {
            r.Id.ToString(CultureInfo.InvariantCulture),
            r.Dog?.Name,
            r.Dog?.Shelter?.Name,
            r.Adopter?.Email,
            r.Status.ToString(),
            FormatDateTime(r.PreferredVisitDateTime),
            r.VisitStatus.ToString(),
            FormatDateTime(r.CreatedAt),
            FormatDateTime(r.UpdatedAt),
            r.ReasonForAdoption,
            r.HoursAlonePerDay?.ToString(CultureInfo.InvariantCulture),
            r.AdditionalInformation
        });

        var file = BuildCsv(
            "pawconnect-adoption-requests",
            ["Request Id", "Dog Name", "Shelter Name", "Adopter Email", "Status", "PreferredVisitDateTime", "VisitStatus", "CreatedAt", "UpdatedAt", "ReasonForAdoption", "HoursAlonePerDay", "AdditionalInformation"],
            rows);
        await LogExportAsync(file, "Admin adoption requests CSV export was generated.", relatedEntityName: "AdoptionRequests");
        return file;
    }

    public async Task<ExportFile> GenerateShelterRequestsCsvAsync()
    {
        var requests = await GetShelterRequestsAsync();
        var rows = requests.Select(r => new[]
        {
            r.Id.ToString(CultureInfo.InvariantCulture),
            r.ShelterName,
            r.ContactPersonName,
            r.Email,
            r.PhoneNumber,
            r.City,
            r.Address,
            r.Status.ToString(),
            FormatDateTime(r.SubmittedAt),
            FormatDateTime(r.ReviewedAt),
            r.ReviewedByUser?.Email
        });

        var file = BuildCsv(
            "pawconnect-shelter-requests",
            ["Request Id", "Shelter Name", "Contact Person", "Email", "Phone", "City", "Address", "Status", "CreatedAt", "ReviewedAt", "ReviewedBy"],
            rows);
        await LogExportAsync(file, "Admin shelter registration requests CSV export was generated.", relatedEntityName: "ShelterRegistrationRequests");
        return file;
    }

    public async Task<ExportFile> GenerateAdoptionRequestsPdfAsync()
    {
        var requests = await GetAdoptionRequestsAsync();

        var bytes = BuildPdf(
            "PawConnect - Adoption Requests Export",
            content =>
            {
                AddSummary(content, [
                    ("Total requests", requests.Count.ToString(CultureInfo.InvariantCulture)),
                    ("Pending", CountAdoptionStatus(requests, AdoptionRequestStatus.Pending)),
                    ("Visit confirmed", CountAdoptionStatus(requests, AdoptionRequestStatus.VisitConfirmed)),
                    ("Accepted", CountAdoptionStatus(requests, AdoptionRequestStatus.Accepted)),
                    ("Rejected", CountAdoptionStatus(requests, AdoptionRequestStatus.Rejected)),
                    ("Cancelled", CountAdoptionStatus(requests, AdoptionRequestStatus.Cancelled))
                ]);

                AddTable(
                    content,
                    ["Dog", "Shelter", "Adopter", "Status", "Visit", "Created", "Updated"],
                    requests.Select(r => new[]
                    {
                        r.Dog?.Name ?? "-",
                        r.Dog?.Shelter?.Name ?? "-",
                        GetAdopterDisplay(r.Adopter),
                        r.Status.ToString(),
                        FormatDateTime(r.PreferredVisitDateTime),
                        FormatDateTime(r.CreatedAt),
                        FormatDateTime(r.UpdatedAt)
                    }));
            });

        var file = BuildPdfExport("pawconnect-adoption-requests", bytes);
        await LogExportAsync(file, "Admin adoption requests PDF export was generated.", relatedEntityName: "AdoptionRequests");
        return file;
    }

    public async Task<ExportFile> GenerateShelterRequestsPdfAsync()
    {
        var requests = await GetShelterRequestsAsync();

        var bytes = BuildPdf(
            "PawConnect - Shelter Registration Requests Export",
            content =>
            {
                AddSummary(content, [
                    ("Total requests", requests.Count.ToString(CultureInfo.InvariantCulture)),
                    ("Pending", CountShelterStatus(requests, ShelterRegistrationRequestStatus.Pending)),
                    ("Accepted", CountShelterStatus(requests, ShelterRegistrationRequestStatus.Accepted)),
                    ("Rejected", CountShelterStatus(requests, ShelterRegistrationRequestStatus.Rejected))
                ]);

                AddTable(
                    content,
                    ["Shelter", "Contact", "Email", "City", "Status", "Submitted", "Reviewed"],
                    requests.Select(r => new[]
                    {
                        r.ShelterName,
                        r.ContactPersonName,
                        r.Email,
                        r.City,
                        r.Status.ToString(),
                        FormatDateTime(r.SubmittedAt),
                        FormatDateTime(r.ReviewedAt)
                    }));
            });

        var file = BuildPdfExport("pawconnect-shelter-requests", bytes);
        await LogExportAsync(file, "Admin shelter registration requests PDF export was generated.", relatedEntityName: "ShelterRegistrationRequests");
        return file;
    }

    public async Task<ExportFile> GenerateShelterDogsCsvAsync(int shelterId)
    {
        var dogs = await context.Dogs
            .Include(d => d.DogBreed)
            .Include(d => d.SecondaryBreed)
            .Include(d => d.PreferredFoodType)
            .Where(d => d.ShelterId == shelterId)
            .OrderBy(d => d.Name)
            .AsNoTracking()
            .ToListAsync();

        var rows = dogs.Select(d => new[]
        {
            d.Id.ToString(CultureInfo.InvariantCulture),
            d.Name,
            DogBreedFormatter.Format(d),
            DogAgeFormatter.Format(d),
            d.Size.ToString(),
            d.Location,
            d.Status.ToString(),
            d.PreferredFoodType?.Name,
            d.DailyFoodAmountGrams?.ToString(CultureInfo.InvariantCulture),
            d.MedicalStatus,
            FormatDate(d.AdoptedAt),
            FormatYesNo(HasSuccessStory(d))
        });

        var file = BuildCsv(
            "pawconnect-shelter-dogs",
            ["Dog Id", "Name", "Breed", "Age", "Size", "Location", "Status", "Preferred Food Type", "Daily Food Amount Grams", "Medical Status", "AdoptedAt", "Has Success Story"],
            rows);
        await LogExportAsync(file, "Shelter dogs CSV export was generated.", shelterId, "Dogs");
        return file;
    }

    public async Task<ExportFile> GenerateShelterAdoptionRequestsCsvAsync(int shelterId)
    {
        var requests = await GetShelterAdoptionRequestsAsync(shelterId);
        var rows = requests.Select(r => new[]
        {
            r.Id.ToString(CultureInfo.InvariantCulture),
            r.Dog?.Name,
            r.Adopter?.Email,
            GetAdopterFullName(r.Adopter),
            r.Status.ToString(),
            FormatDateTime(r.PreferredVisitDateTime),
            r.VisitStatus.ToString(),
            FormatDateTime(r.CreatedAt),
            FormatDateTime(r.UpdatedAt),
            r.ReasonForAdoption,
            r.HoursAlonePerDay?.ToString(CultureInfo.InvariantCulture),
            r.AdditionalInformation,
            r.ShelterInternalNotes
        });

        var file = BuildCsv(
            "pawconnect-shelter-adoption-requests",
            ["Request Id", "Dog Name", "Adopter Email", "Adopter Full Name", "Status", "PreferredVisitDateTime", "VisitStatus", "CreatedAt", "UpdatedAt", "ReasonForAdoption", "HoursAlonePerDay", "AdditionalInformation", "ShelterInternalNotes"],
            rows);
        await LogExportAsync(file, "Shelter adoption requests CSV export was generated.", shelterId, "AdoptionRequests");
        return file;
    }

    public async Task<ExportFile> GenerateShelterAdoptionRequestsPdfAsync(int shelterId)
    {
        var shelter = await GetShelterByIdAsync(shelterId);
        var requests = await GetShelterAdoptionRequestsAsync(shelterId);

        var bytes = BuildPdf(
            "PawConnect - Shelter Adoption Requests Export",
            content =>
            {
                AddSummary(content, [
                    ("Shelter", shelter?.Name ?? "Current shelter"),
                    ("Total", requests.Count.ToString(CultureInfo.InvariantCulture)),
                    ("Pending", CountAdoptionStatus(requests, AdoptionRequestStatus.Pending)),
                    ("Visit confirmed", CountAdoptionStatus(requests, AdoptionRequestStatus.VisitConfirmed)),
                    ("Accepted", CountAdoptionStatus(requests, AdoptionRequestStatus.Accepted)),
                    ("Rejected", CountAdoptionStatus(requests, AdoptionRequestStatus.Rejected)),
                    ("Cancelled", CountAdoptionStatus(requests, AdoptionRequestStatus.Cancelled))
                ]);

                AddTable(
                    content,
                    ["Dog", "Adopter", "Status", "Visit", "Created", "Updated"],
                    requests.Select(r => new[]
                    {
                        r.Dog?.Name ?? "-",
                        GetAdopterDisplay(r.Adopter),
                        r.Status.ToString(),
                        FormatDateTime(r.PreferredVisitDateTime),
                        FormatDateTime(r.CreatedAt),
                        FormatDateTime(r.UpdatedAt)
                    }));

                AddTable(
                    content,
                    ["Dog", "Reason", "Hours Alone", "Internal Notes"],
                    requests.Select(r => new[]
                    {
                        r.Dog?.Name ?? "-",
                        r.ReasonForAdoption,
                        r.HoursAlonePerDay?.ToString(CultureInfo.InvariantCulture),
                        r.ShelterInternalNotes
                    }));
            });

        var file = BuildPdfExport("pawconnect-shelter-adoption-requests", bytes);
        await LogExportAsync(file, "Shelter adoption requests PDF export was generated.", shelterId, "AdoptionRequests");
        return file;
    }

    public async Task<ExportFile> GenerateShelterResourcesCsvAsync(int shelterId)
    {
        var resources = await GetShelterResourcesAsync(shelterId);
        var rows = resources.Select(r => new[]
        {
            r.Id.ToString(CultureInfo.InvariantCulture),
            r.Name,
            r.ResourceCategory?.Name,
            r.FoodType?.Name,
            r.Quantity.ToString(CultureInfo.InvariantCulture),
            r.Unit,
            r.LowStockThreshold.ToString(CultureInfo.InvariantCulture),
            FormatYesNo(IsLowStock(r)),
            FormatDateTime(r.LastUpdatedAt)
        });

        var file = BuildCsv(
            "pawconnect-shelter-resources",
            ["Resource Id", "Name", "Category", "Food Type", "Quantity", "Unit", "LowStockThreshold", "IsLowStock", "LastUpdatedAt"],
            rows);
        await LogExportAsync(file, "Shelter resources CSV export was generated.", shelterId, "Resources");
        return file;
    }

    public async Task<ExportFile> GenerateShelterResourcesPdfAsync(int shelterId)
    {
        var shelter = await GetShelterByIdAsync(shelterId);
        var resources = await GetShelterResourcesAsync(shelterId);

        var bytes = BuildPdf(
            "PawConnect - Shelter Resource Stock Export",
            content =>
            {
                AddSummary(content, [
                    ("Shelter", shelter?.Name ?? "Current shelter"),
                    ("Total resources", resources.Count.ToString(CultureInfo.InvariantCulture)),
                    ("Low stock", resources.Count(IsLowStock).ToString(CultureInfo.InvariantCulture))
                ]);

                AddTable(
                    content,
                    ["Resource", "Category", "Food Type", "Quantity", "Unit", "Threshold", "Low Stock"],
                    resources.Select(r => new[]
                    {
                        r.Name,
                        r.ResourceCategory?.Name ?? "-",
                        r.FoodType?.Name ?? "-",
                        r.Quantity.ToString(CultureInfo.InvariantCulture),
                        r.Unit,
                        r.LowStockThreshold.ToString(CultureInfo.InvariantCulture),
                        FormatYesNo(IsLowStock(r))
                    }));
            });

        var file = BuildPdfExport("pawconnect-shelter-resources", bytes);
        await LogExportAsync(file, "Shelter resources PDF export was generated.", shelterId, "Resources");
        return file;
    }

    private Task<List<AdoptionRequest>> GetAdoptionRequestsAsync()
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    private Task<List<AdoptionRequest>> GetShelterAdoptionRequestsAsync(int shelterId)
    {
        return context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .Where(r => r.Dog != null && r.Dog.ShelterId == shelterId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    private Task<List<ResourceStock>> GetShelterResourcesAsync(int shelterId)
    {
        return context.ResourceStocks
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .Where(r => r.ShelterId == shelterId)
            .OrderBy(r => r.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    private Task<Shelter?> GetShelterByIdAsync(int shelterId)
    {
        return context.Shelters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shelterId);
    }

    private Task<List<ShelterRegistrationRequest>> GetShelterRequestsAsync()
    {
        return context.ShelterRegistrationRequests
            .Include(r => r.ReviewedByUser)
            .OrderByDescending(r => r.SubmittedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    private static ExportFile BuildCsv(string filePrefix, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        AppendCsvRow(builder, headers);

        foreach (var row in rows)
        {
            AppendCsvRow(builder, row);
        }

        return new ExportFile(BuildFileName(filePrefix, "csv"), CsvContentType, CsvEncoding.GetBytes(builder.ToString()));
    }

    private static ExportFile BuildPdfExport(string filePrefix, byte[] content)
    {
        return new ExportFile(BuildFileName(filePrefix, "pdf"), PdfContentType, content);
    }

    private async Task LogExportAsync(ExportFile file, string description, int? shelterId = null, string? relatedEntityName = null)
    {
        var additionalData = shelterId.HasValue
            ? $"FileName={file.FileName};ContentType={file.ContentType};ShelterId={shelterId.Value}"
            : $"FileName={file.FileName};ContentType={file.ContentType}";

        if (auditLogService is not null)
        {
            await auditLogService.LogAsync(
                AuditActions.ExportGenerated,
                "Export",
                file.FileName,
                description,
                additionalData: additionalData);
        }

        if (reportHistoryService is not null)
        {
            await reportHistoryService.RecordReportGeneratedAsync(new ReportHistoryRecord(
                IsPdf(file) ? ReportHistoryTypes.PdfExport : ReportHistoryTypes.CsvExport,
                shelterId.HasValue ? ReportHistoryTriggers.Shelter : ReportHistoryTriggers.Admin,
                Subject: description,
                FileName: file.FileName,
                GeneratedAt: DateTime.UtcNow,
                ShelterId: shelterId,
                RelatedEntityName: relatedEntityName ?? "Export",
                RelatedEntityId: file.FileName));
        }
    }

    private static bool IsPdf(ExportFile file)
    {
        return file.ContentType.Equals(PdfContentType, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFileName(string filePrefix, string extension)
    {
        return $"{filePrefix}-{DateTime.Today:yyyy-MM-dd}.{extension}";
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
        if (normalized.Contains('"') || normalized.Contains(',') || normalized.Contains('\n') || normalized.Contains('\r'))
        {
            return $"\"{normalized.Replace("\"", "\"\"")}\"";
        }

        return normalized;
    }

    private byte[] BuildPdf(string title, Action<ColumnDescriptor> buildContent)
    {
        try
        {
            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(36);
                    page.DefaultTextStyle(text => text.FontSize(9).FontColor(Colors.Grey.Darken3));

                    page.Header().Element(container => ComposeHeader(container, title));
                    page.Content().PaddingTop(16).Column(column =>
                    {
                        column.Spacing(14);
                        buildContent(column);
                    });
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Export PDF generation failed for {Title}.", title);
            throw;
        }
    }

    private static void ComposeHeader(IContainer container, string title)
    {
        container.Column(column =>
        {
            column.Item().Text("PawConnect")
                .FontSize(22)
                .Bold()
                .FontColor(Colors.Green.Darken3);

            column.Item().PaddingTop(4).Text(title)
                .FontSize(15)
                .SemiBold()
                .FontColor(Colors.Grey.Darken4);

            column.Item().PaddingTop(6).Text($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
                .FontSize(9)
                .FontColor(Colors.Grey.Darken1);

            column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Green.Lighten1);
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text("Export generated by PawConnect.")
            .FontSize(8)
            .FontColor(Colors.Grey.Darken1);
    }

    private static void AddSummary(ColumnDescriptor content, IReadOnlyList<(string Label, string Value)> rows)
    {
        content.Item().Text("Summary")
            .FontSize(13)
            .Bold()
            .FontColor(Colors.Green.Darken3);

        content.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var _ in rows)
                {
                    columns.RelativeColumn();
                }
            });

            foreach (var (label, _) in rows)
            {
                table.Cell().Element(HeaderCell).Text(label);
            }

            foreach (var (_, value) in rows)
            {
                table.Cell().Element(ValueCell).Text(value);
            }
        });
    }

    private static void AddTable(ColumnDescriptor content, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        content.Item().Text("Records")
            .FontSize(13)
            .Bold()
            .FontColor(Colors.Green.Darken3);

        content.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var _ in headers)
                {
                    columns.RelativeColumn();
                }
            });

            foreach (var header in headers)
            {
                table.Cell().Element(HeaderCell).Text(header);
            }

            foreach (var row in rows)
            {
                foreach (var value in row)
                {
                    table.Cell().Element(ValueCell).Text(string.IsNullOrWhiteSpace(value) ? "-" : value);
                }
            }
        });
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container
            .Background(Colors.Green.Lighten4)
            .BorderBottom(1)
            .BorderColor(Colors.Green.Lighten2)
            .PaddingVertical(6)
            .PaddingHorizontal(6);
    }

    private static IContainer ValueCell(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(5)
            .PaddingHorizontal(6);
    }

    private static string CountAdoptionStatus(IEnumerable<AdoptionRequest> requests, AdoptionRequestStatus status)
    {
        return requests.Count(r => r.Status == status).ToString(CultureInfo.InvariantCulture);
    }

    private static string CountShelterStatus(IEnumerable<ShelterRegistrationRequest> requests, ShelterRegistrationRequestStatus status)
    {
        return requests.Count(r => r.Status == status).ToString(CultureInfo.InvariantCulture);
    }

    private static string GetAdopterDisplay(ApplicationUser? adopter)
    {
        return string.IsNullOrWhiteSpace(adopter?.FullName)
            ? adopter?.Email ?? "-"
            : $"{adopter.FullName} ({adopter.Email})";
    }

    private static string? GetAdopterFullName(ApplicationUser? adopter)
    {
        if (!string.IsNullOrWhiteSpace(adopter?.AdopterProfile?.FullName))
        {
            return adopter.AdopterProfile.FullName;
        }

        return string.IsNullOrWhiteSpace(adopter?.FullName) ? null : adopter.FullName;
    }

    private static bool HasSuccessStory(Dog dog)
    {
        return !string.IsNullOrWhiteSpace(dog.SuccessStoryText) || dog.AdoptedAt.HasValue;
    }

    private static bool IsLowStock(ResourceStock resource)
    {
        return resource.Quantity <= resource.LowStockThreshold;
    }

    private static string FormatYesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static string? FormatDate(DateTime? value)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string? FormatDateTime(DateTime? value)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string? FormatCoordinate(double? value)
    {
        return value?.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
