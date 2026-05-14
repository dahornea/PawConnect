using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PawConnect.Services;

public class PdfReportService(ApplicationDbContext context, ILogger<PdfReportService> logger) : IPdfReportService
{
    static PdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateAdoptionRequestReportAsync(int adoptionRequestId)
    {
        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .ThenInclude(a => a!.AdopterProfile)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == adoptionRequestId);

        if (request is null)
        {
            throw new InvalidOperationException("Adoption request was not found.");
        }

        var profile = request.Adopter?.AdopterProfile;
        var dog = request.Dog;

        return BuildReport(
            "PawConnect - Adoption Request Report",
            content =>
            {
                AddSection(content, "Dog Information", [
                    ("Dog name", dog?.Name),
                    ("Breed", dog?.Breed),
                    ("Age", dog is null ? null : DogAgeFormatter.Format(dog)),
                    ("Size", dog?.Size.ToString()),
                    ("Current status", dog?.Status.ToString()),
                    ("Shelter name", dog?.Shelter?.Name)
                ]);

                AddSection(content, "Adopter Information", [
                    ("Full name", profile?.FullName ?? request.Adopter?.FullName),
                    ("Email", request.Adopter?.Email),
                    ("Phone number", profile?.PhoneNumber ?? request.Adopter?.PhoneNumber),
                    ("City", profile?.City),
                    ("Housing type", profile?.HousingType.ToString()),
                    ("Has yard", FormatYesNo(profile?.HasYard)),
                    ("Has other pets", FormatYesNo(profile?.HasOtherPets)),
                    ("Has children", FormatYesNo(profile?.HasChildren)),
                    ("Experience with dogs", profile?.ExperienceWithDogs)
                ]);

                AddSection(content, "Request Details", [
                    ("Reason for adoption", request.ReasonForAdoption),
                    ("Hours alone per day", request.HoursAlonePerDay?.ToString() ?? "Not provided"),
                    ("Additional information", request.AdditionalInformation),
                    ("Preferred visit", VisitSchedulingHelper.FormatVisitDateTime(request.PreferredVisitDateTime)),
                    ("Visit status", request.VisitStatus.ToString()),
                    ("Request date", FormatDateTime(request.CreatedAt))
                ]);
            });
    }

    public async Task<byte[]> GenerateAdoptionStatusReportAsync(int adoptionRequestId)
    {
        var request = await context.AdoptionRequests
            .Include(r => r.Dog)
            .ThenInclude(d => d!.Shelter)
            .Include(r => r.Adopter)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == adoptionRequestId);

        if (request is null)
        {
            throw new InvalidOperationException("Adoption request was not found.");
        }

        var dog = request.Dog;
        var shelter = dog?.Shelter;
        var isAccepted = request.Status == AdoptionRequestStatus.Accepted;

        return BuildReport(
            "PawConnect - Adoption Status Report",
            content =>
            {
                AddSection(content, "Summary", [
                    ("Message", $"Hello {GetAdopterDisplayName(request.Adopter)}, your adoption request has been {request.Status.ToString().ToLowerInvariant()}."),
                    ("Dog name", dog?.Name),
                    ("New adoption request status", request.Status.ToString()),
                    ("Status update date", FormatDateTime(request.UpdatedAt))
                ]);

                AddSection(content, "Dog Information", [
                    ("Dog name", dog?.Name),
                    ("Breed", dog?.Breed),
                    ("Age", dog is null ? null : DogAgeFormatter.Format(dog)),
                    ("Size", dog?.Size.ToString()),
                    ("Shelter name", shelter?.Name)
                ]);

                AddSection(content, "Shelter Contact", [
                    ("Shelter name", shelter?.Name),
                    ("Shelter email", shelter?.Email),
                    ("Shelter phone number", shelter?.PhoneNumber),
                    ("Shelter city", shelter?.City)
                ]);

                AddSection(content, "Next Steps", [
                    ("Recommendation", isAccepted
                        ? "The shelter has marked the adoption as completed after the visit."
                        : "You can continue browsing other dogs on PawConnect and submit a new request when you find another good match.")
                ]);
            });
    }

    public async Task<byte[]> GenerateLowStockResourceReportAsync(int resourceStockId)
    {
        var resource = await context.ResourceStocks
            .Include(r => r.Shelter)
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == resourceStockId);

        if (resource is null)
        {
            throw new InvalidOperationException("Resource stock item was not found.");
        }

        return BuildReport(
            "PawConnect - Low Stock Resource Report",
            content =>
            {
                AddSection(content, "Shelter Information", [
                    ("Shelter name", resource.Shelter?.Name),
                    ("City", resource.Shelter?.City),
                    ("Email", resource.Shelter?.Email)
                ]);

                AddSection(content, "Resource Information", [
                    ("Resource name", resource.Name),
                    ("Category", resource.ResourceCategory?.Name),
                    ("Food type", resource.FoodType?.Name),
                    ("Current quantity", resource.Quantity.ToString("0.##")),
                    ("Unit", resource.Unit),
                    ("Low-stock threshold", resource.LowStockThreshold.ToString("0.##"))
                ]);

                AddSection(content, "Recommendation", [
                    ("Action needed", "Please review this resource and update your shelter inventory when new stock is available.")
                ]);
            });
    }

    public async Task<byte[]> GenerateShelterRegistrationRequestReportAsync(int shelterRegistrationRequestId)
    {
        var request = await context.ShelterRegistrationRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == shelterRegistrationRequestId);

        if (request is null)
        {
            throw new InvalidOperationException("Shelter registration request was not found.");
        }

        return BuildReport(
            "PawConnect - Shelter Registration Request",
            content =>
            {
                AddSection(content, "Shelter Information", [
                    ("Shelter name", request.ShelterName),
                    ("Contact person", request.ContactPersonName),
                    ("Email", request.Email),
                    ("Phone", request.PhoneNumber),
                    ("City", request.City),
                    ("Address", request.Address),
                    ("Description", request.Description)
                ]);

                AddSection(content, "Additional Details", [
                    ("Website", request.Website),
                    ("Opening hours", request.OpeningHours),
                    ("Reason for joining", request.ReasonForJoining),
                    ("Latitude", request.Latitude?.ToString("0.######")),
                    ("Longitude", request.Longitude?.ToString("0.######")),
                    ("Submitted date", FormatDateTime(request.SubmittedAt)),
                    ("Status", request.Status.ToString())
                ]);
            });
    }

    public async Task<byte[]> GenerateShelterSummaryReportAsync(int shelterId, DateTime fromDate, DateTime toDate)
    {
        var shelter = await context.Shelters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shelterId);

        if (shelter is null)
        {
            throw new InvalidOperationException("Shelter was not found.");
        }

        var requests = await context.AdoptionRequests
            .Where(r => r.Dog != null && r.Dog.ShelterId == shelterId)
            .Select(r => new
            {
                r.Status,
                r.CreatedAt
            })
            .AsNoTracking()
            .ToListAsync();

        var dogs = await context.Dogs
            .Where(d => d.ShelterId == shelterId)
            .Select(d => new ShelterSummaryDogRow(d.Name, d.Breed, d.Status, d.AdoptedAt))
            .AsNoTracking()
            .ToListAsync();

        var lowStockResources = await context.ResourceStocks
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .Where(r => r.ShelterId == shelterId && r.Quantity <= r.LowStockThreshold)
            .OrderBy(r => r.Quantity)
            .ThenBy(r => r.Name)
            .AsNoTracking()
            .ToListAsync();

        var recentlyAdoptedDogs = dogs
            .Where(d => d.AdoptedAt.HasValue && d.AdoptedAt.Value >= fromDate && d.AdoptedAt.Value <= toDate)
            .OrderByDescending(d => d.AdoptedAt)
            .ToList();

        return BuildReport(
            "PawConnect - Shelter Summary Report",
            content =>
            {
                AddSection(content, "Shelter Information", [
                    ("Shelter name", shelter.Name),
                    ("Shelter email", shelter.Email),
                    ("City", shelter.City),
                    ("Report generated", FormatDateTime(toDate)),
                    ("Report period", $"{FormatDateTime(fromDate)} - {FormatDateTime(toDate)}")
                ]);

                AddSection(content, "Adoption Request Summary", [
                    ("New requests in period", requests.Count(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate).ToString()),
                    ("Pending requests", requests.Count(r => r.Status == AdoptionRequestStatus.Pending).ToString()),
                    ("Accepted requests", requests.Count(r => r.Status == AdoptionRequestStatus.Accepted).ToString()),
                    ("Rejected requests", requests.Count(r => r.Status == AdoptionRequestStatus.Rejected).ToString()),
                    ("Cancelled requests", requests.Count(r => r.Status == AdoptionRequestStatus.Cancelled).ToString()),
                    ("Total requests", requests.Count.ToString())
                ]);

                AddSection(content, "Dog Overview", [
                    ("Total dogs", dogs.Count.ToString()),
                    ("Available dogs", dogs.Count(d => d.Status == DogStatus.Available).ToString()),
                    ("Reserved dogs", dogs.Count(d => d.Status == DogStatus.Reserved).ToString()),
                    ("Adopted dogs", dogs.Count(d => d.Status == DogStatus.Adopted).ToString()),
                    ("In treatment dogs", dogs.Count(d => d.Status == DogStatus.InTreatment).ToString()),
                    ("Recently adopted dogs", recentlyAdoptedDogs.Count.ToString())
                ]);

                AddSection(content, "Recently Adopted Dogs", BuildRecentlyAdoptedRows(recentlyAdoptedDogs));
                AddSection(content, "Low-Stock Resources", BuildLowStockRows(lowStockResources));
                AddSection(content, "Report Note", [
                    ("Note", "This report was generated automatically by PawConnect.")
                ]);
            });
    }

    private byte[] BuildReport(string title, Action<ColumnDescriptor> buildContent)
    {
        try
        {
            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(42);
                    page.DefaultTextStyle(text => text.FontSize(10).FontColor(Colors.Grey.Darken3));

                    page.Header().Element(container => ComposeHeader(container, title));
                    page.Content().PaddingTop(20).Column(column =>
                    {
                        column.Spacing(16);
                        buildContent(column);
                    });
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PDF report generation failed for {Title}.", title);
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

            column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Green.Lighten1);
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Generated by PawConnect on ").FontColor(Colors.Grey.Darken1);
            text.Span(DateTime.Now.ToString("dd MMM yyyy HH:mm")).SemiBold().FontColor(Colors.Grey.Darken2);
        });
    }

    private static void AddSection(ColumnDescriptor content, string heading, IEnumerable<(string Label, string? Value)> rows)
    {
        content.Item().EnsureSpace(90).Column(section =>
        {
            section.Spacing(6);
            section.Item().Text(heading)
                .FontSize(13)
                .Bold()
                .FontColor(Colors.Green.Darken3);

            section.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(2.7f);
                });

                foreach (var (label, value) in rows)
                {
                    table.Cell().Element(LabelCell).Text(label);
                    table.Cell().Element(ValueCell).Text(string.IsNullOrWhiteSpace(value) ? "-" : value);
                }
            });
        });
    }

    private static IContainer LabelCell(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .PaddingVertical(6)
            .PaddingHorizontal(8);
    }

    private static IContainer ValueCell(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(6)
            .PaddingHorizontal(8);
    }

    private static string? FormatYesNo(bool? value)
    {
        return value.HasValue ? (value.Value ? "Yes" : "No") : null;
    }

    private static string FormatDateTime(DateTime dateTime)
    {
        return dateTime.ToLocalTime().ToString("dd MMM yyyy HH:mm");
    }

    private static string GetAdopterDisplayName(ApplicationUser? adopter)
    {
        return string.IsNullOrWhiteSpace(adopter?.FullName)
            ? adopter?.Email ?? "there"
            : adopter.FullName;
    }

    private static IEnumerable<(string Label, string? Value)> BuildRecentlyAdoptedRows(IEnumerable<ShelterSummaryDogRow> recentlyAdoptedDogs)
    {
        var rows = recentlyAdoptedDogs
            .Select<ShelterSummaryDogRow, (string Label, string? Value)>(d => (d.Name, $"{d.Breed} | Adopted: {FormatDateTime(d.AdoptedAt!.Value)}"))
            .ToList();

        return rows.Count == 0
            ? [("Status", "No dogs were adopted during this report period.")]
            : rows;
    }

    private static IEnumerable<(string Label, string? Value)> BuildLowStockRows(IEnumerable<ResourceStock> resources)
    {
        var rows = resources
            .Select<ResourceStock, (string Label, string? Value)>(resource =>
            {
                var category = resource.ResourceCategory?.Name ?? "Unknown category";
                var foodType = resource.FoodType is null ? null : $" | Food type: {resource.FoodType.Name}";
                return (
                    resource.Name,
                    $"{category}{foodType} | Current: {resource.Quantity} {resource.Unit} | Threshold: {resource.LowStockThreshold} {resource.Unit}");
            })
            .ToList();

        return rows.Count == 0
            ? [("Status", "No resources are currently at or below their low-stock threshold.")]
            : rows;
    }

    private sealed record ShelterSummaryDogRow(string Name, string Breed, DogStatus Status, DateTime? AdoptedAt);
}
