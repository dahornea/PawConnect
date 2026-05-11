using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;
using PawConnect.Tests.Tests.Helpers;

namespace PawConnect.Tests.Tests;

public class ResourceStockServiceTests
{
    [Fact]
    public async Task CreateResourceAsync_CreatesShelterResource()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = CreateService(context);

        await service.CreateResourceAsync(new ResourceStock
        {
            Name = "Adult Food",
            ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
            FoodTypeId = TestDbContextFactory.AdultFoodTypeId,
            Quantity = 20,
            Unit = "kg",
            LowStockThreshold = 5
        }, TestDbContextFactory.ShelterId);

        var resource = await context.ResourceStocks.SingleAsync();
        Assert.Equal(TestDbContextFactory.ShelterId, resource.ShelterId);
        Assert.Equal(TestDbContextFactory.AdultFoodTypeId, resource.FoodTypeId);
    }

    [Fact]
    public async Task UpdateResourceAsync_BlocksAnotherSheltersResource()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var resource = SeedResource(context, TestDbContextFactory.ShelterId);
        var service = CreateService(context);

        resource.Name = "Changed";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateResourceAsync(resource, TestDbContextFactory.OtherShelterId));

        Assert.Equal("Resource stock item was not found for your shelter.", exception.Message);
    }

    [Fact]
    public async Task DeleteResourceAsync_RemovesOwnResource()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var resource = SeedResource(context, TestDbContextFactory.ShelterId);
        var service = CreateService(context);

        await service.DeleteResourceAsync(resource.Id, TestDbContextFactory.ShelterId);

        Assert.False(await context.ResourceStocks.AnyAsync(r => r.Id == resource.Id));
    }

    [Fact]
    public async Task GetLowStockResourcesForShelterAsync_ReturnsQuantityAtOrBelowThreshold()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.ResourceStocks.AddRange(
            Resource("Below", 2, 5),
            Resource("Equal", 5, 5),
            Resource("Above", 8, 5));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var lowStock = await service.GetLowStockResourcesForShelterAsync(TestDbContextFactory.ShelterId);

        Assert.Contains(lowStock, r => r.Name == "Below");
        Assert.Contains(lowStock, r => r.Name == "Equal");
        Assert.DoesNotContain(lowStock, r => r.Name == "Above");
    }

    [Fact]
    public async Task UpdateResourceAsync_ClearsFoodTypeForNonFoodCategory()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var resource = SeedResource(context, TestDbContextFactory.ShelterId);
        var service = CreateService(context);

        resource.ResourceCategoryId = TestDbContextFactory.MedicineCategoryId;
        resource.FoodTypeId = TestDbContextFactory.AdultFoodTypeId;

        await service.UpdateResourceAsync(resource, TestDbContextFactory.ShelterId);

        Assert.Null((await context.ResourceStocks.FindAsync(resource.Id))!.FoodTypeId);
    }

    private static ResourceStockService CreateService(ApplicationDbContext context)
    {
        return new ResourceStockService(
            context,
            new TestEmailService(),
            new TestPdfReportService(),
            NullLogger<ResourceStockService>.Instance);
    }

    private static ResourceStock SeedResource(ApplicationDbContext context, int shelterId)
    {
        var resource = Resource("Adult Food", 10, 5);
        resource.ShelterId = shelterId;
        context.ResourceStocks.Add(resource);
        context.SaveChanges();
        return resource;
    }

    private static ResourceStock Resource(string name, int quantity, int threshold)
    {
        return new ResourceStock
        {
            ShelterId = TestDbContextFactory.ShelterId,
            Name = name,
            Quantity = quantity,
            Unit = "kg",
            LowStockThreshold = threshold,
            ResourceCategoryId = TestDbContextFactory.FoodCategoryId,
            FoodTypeId = TestDbContextFactory.AdultFoodTypeId
        };
    }
}
