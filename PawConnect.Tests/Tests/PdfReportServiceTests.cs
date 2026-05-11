using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class PdfReportServiceTests
{
    [Fact]
    public async Task GenerateAdoptionRequestReportAsync_ReturnsPdfBytes()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = SeedAdoptionRequest(context);
        var service = new PdfReportService(context, NullLogger<PdfReportService>.Instance);

        var pdf = await service.GenerateAdoptionRequestReportAsync(request.Id);

        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, Math.Min(4, pdf.Length)));
    }

    [Fact]
    public async Task GenerateAdoptionStatusReportAsync_ReturnsPdfBytes()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var request = SeedAdoptionRequest(context);
        request.Status = AdoptionRequestStatus.Accepted;
        request.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        var service = new PdfReportService(context, NullLogger<PdfReportService>.Instance);

        var pdf = await service.GenerateAdoptionStatusReportAsync(request.Id);

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task GenerateLowStockResourceReportAsync_ReturnsPdfBytes()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var resource = new ResourceStock
        {
            ShelterId = TestDbContextFactory.ShelterId,
            Name = "Medicine Kits",
            Quantity = 1,
            Unit = "pcs",
            LowStockThreshold = 3,
            ResourceCategoryId = TestDbContextFactory.MedicineCategoryId
        };
        context.ResourceStocks.Add(resource);
        await context.SaveChangesAsync();
        var service = new PdfReportService(context, NullLogger<PdfReportService>.Instance);

        var pdf = await service.GenerateLowStockResourceReportAsync(resource.Id);

        Assert.NotEmpty(pdf);
    }

    private static AdoptionRequest SeedAdoptionRequest(ApplicationDbContext context)
    {
        var dog = TestDbContextFactory.CreateDog("PDF Dog", DogStatus.Available);
        context.Dogs.Add(dog);
        context.SaveChanges();

        var request = new AdoptionRequest
        {
            DogId = dog.Id,
            AdopterId = TestDbContextFactory.AdopterId,
            ReasonForAdoption = "I can provide a stable home.",
            AdditionalInformation = "Optional fields should be safe.",
            Status = AdoptionRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AdoptionRequests.Add(request);
        context.SaveChanges();
        return request;
    }
}
