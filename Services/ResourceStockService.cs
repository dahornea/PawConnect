using Microsoft.EntityFrameworkCore;
using PawConnect.Data;
using PawConnect.Entities;

namespace PawConnect.Services;

public class ResourceStockService(
    ApplicationDbContext context,
    IEmailService emailService,
    IPdfReportService pdfReportService,
    ILogger<ResourceStockService> logger,
    IAuditLogService? auditLogService = null,
    INotificationService? notificationService = null) : IResourceStockService
{
    public Task<List<ResourceStock>> GetAllAsync()
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<ResourceStock?> GetByIdAsync(int id)
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task CreateAsync(ResourceStock resourceStock)
    {
        context.ResourceStocks.Add(resourceStock);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ResourceStock resourceStock)
    {
        resourceStock.LastUpdatedAt = DateTime.UtcNow;
        context.ResourceStocks.Update(resourceStock);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var resourceStock = await context.ResourceStocks.FindAsync(id);
        if (resourceStock is null)
        {
            return;
        }

        context.ResourceStocks.Remove(resourceStock);
        await context.SaveChangesAsync();
    }

    public Task<List<ResourceStock>> GetForShelterAsync(int shelterId)
    {
        return GetResourcesForShelterAsync(shelterId);
    }

    public Task<List<ResourceStock>> GetResourcesForShelterAsync(int shelterId)
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .Where(r => r.ShelterId == shelterId)
            .OrderBy(r => r.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<ResourceStock>> GetLowStockResourcesForShelterAsync(int shelterId)
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .Where(r => r.ShelterId == shelterId && r.Quantity <= r.LowStockThreshold)
            .OrderBy(r => r.Quantity)
            .ThenBy(r => r.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<ResourceStock?> GetResourceForShelterAsync(int resourceId, int shelterId)
    {
        return context.ResourceStocks
            .Include(r => r.Shelter)
            .Include(r => r.ResourceCategory)
            .Include(r => r.FoodType)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == resourceId && r.ShelterId == shelterId);
    }

    public async Task CreateResourceAsync(ResourceStock resource, int shelterId)
    {
        await PrepareResourceAsync(resource, shelterId);

        resource.Id = 0;
        resource.ShelterId = shelterId;
        resource.Shelter = null;
        resource.LastUpdatedAt = DateTime.UtcNow;

        context.ResourceStocks.Add(resource);
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.ResourceCreated,
            "ResourceStock",
            resource.Id.ToString(),
            $"Resource {resource.Name} was created.",
            additionalData: $"ShelterId={shelterId}");
        await NotifyShelterIfLowStockAsync(resource.Id, "created");
    }

    public async Task UpdateResourceAsync(ResourceStock resource, int shelterId)
    {
        var existingResource = await context.ResourceStocks.FirstOrDefaultAsync(r => r.Id == resource.Id && r.ShelterId == shelterId);
        if (existingResource is null)
        {
            throw new InvalidOperationException("Resource stock item was not found for your shelter.");
        }

        await PrepareResourceAsync(resource, shelterId, resource.Id);

        existingResource.Name = resource.Name.Trim();
        existingResource.ResourceCategoryId = resource.ResourceCategoryId;
        existingResource.FoodTypeId = resource.FoodTypeId;
        existingResource.Quantity = resource.Quantity;
        existingResource.Unit = resource.Unit.Trim();
        existingResource.LowStockThreshold = resource.LowStockThreshold;
        existingResource.LastUpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.ResourceUpdated,
            "ResourceStock",
            existingResource.Id.ToString(),
            $"Resource {existingResource.Name} was updated.",
            additionalData: $"ShelterId={shelterId}");
        await NotifyShelterIfLowStockAsync(existingResource.Id, "updated");
    }

    public async Task DeleteResourceAsync(int resourceId, int shelterId)
    {
        var resource = await context.ResourceStocks.FirstOrDefaultAsync(r => r.Id == resourceId && r.ShelterId == shelterId);
        if (resource is null)
        {
            throw new InvalidOperationException("Resource stock item was not found for your shelter.");
        }

        context.ResourceStocks.Remove(resource);
        await context.SaveChangesAsync();
        await LogAsync(
            AuditActions.ResourceDeleted,
            "ResourceStock",
            resourceId.ToString(),
            $"Resource {resource.Name} was deleted.",
            additionalData: $"ShelterId={shelterId}");
    }

    private async Task PrepareResourceAsync(ResourceStock resource, int shelterId, int? currentResourceId = null)
    {
        ValidateResource(resource);

        var category = await context.ResourceCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == resource.ResourceCategoryId);
        if (category is null)
        {
            throw new InvalidOperationException("Resource category is required.");
        }

        if (!category.Name.Equals("Food", StringComparison.OrdinalIgnoreCase))
        {
            resource.FoodTypeId = null;
        }
        else if (!resource.FoodTypeId.HasValue)
        {
            throw new InvalidOperationException("Food type is required for food resources.");
        }
        else if (resource.FoodTypeId.HasValue)
        {
            var foodTypeExists = await context.FoodTypes.AnyAsync(f => f.Id == resource.FoodTypeId.Value);
            if (!foodTypeExists)
            {
                throw new InvalidOperationException("Selected food type was not found.");
            }
        }

        resource.Name = resource.Name.Trim();
        resource.Unit = resource.Unit.Trim();

        var normalizedName = resource.Name.ToUpperInvariant();
        var duplicateExists = await context.ResourceStocks.AnyAsync(r =>
            r.ShelterId == shelterId &&
            (!currentResourceId.HasValue || r.Id != currentResourceId.Value) &&
            r.ResourceCategoryId == resource.ResourceCategoryId &&
            r.FoodTypeId == resource.FoodTypeId &&
            r.Name.Trim().ToUpper() == normalizedName);

        if (duplicateExists)
        {
            throw new InvalidOperationException("This resource already exists in your shelter stock.");
        }
    }

    private static void ValidateResource(ResourceStock resource)
    {
        if (string.IsNullOrWhiteSpace(resource.Name))
        {
            throw new InvalidOperationException("Resource name is required.");
        }

        if (resource.ResourceCategoryId <= 0)
        {
            throw new InvalidOperationException("Resource category is required.");
        }

        if (resource.Quantity < 0)
        {
            throw new InvalidOperationException("Quantity must be zero or greater.");
        }

        if (resource.LowStockThreshold < 0)
        {
            throw new InvalidOperationException("Low-stock threshold must be zero or greater.");
        }

        if (string.IsNullOrWhiteSpace(resource.Unit))
        {
            throw new InvalidOperationException("Unit is required.");
        }
    }

    private async Task NotifyShelterIfLowStockAsync(int resourceId, string action)
    {
        try
        {
            var resource = await context.ResourceStocks
                .Include(r => r.ResourceCategory)
                .Include(r => r.FoodType)
                .Include(r => r.Shelter)
                .ThenInclude(s => s!.ApplicationUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == resourceId);

            if (resource is null || resource.Quantity > resource.LowStockThreshold)
            {
                return;
            }

            var shelterEmail = resource.Shelter?.ApplicationUser?.Email ?? resource.Shelter?.Email;
            if (resource.Shelter?.ApplicationUserId is not null && notificationService is not null)
            {
                await notificationService.CreateNotificationAsync(
                    resource.Shelter.ApplicationUserId,
                    "Low stock resource",
                    $"{resource.Name} is at or below the low-stock threshold.",
                    NotificationCategory.Resource,
                    NotificationType.Warning,
                    "/shelter/resources",
                    "ResourceStock",
                    resource.Id.ToString());
            }

            var body = $"""
                Hello,

                A shelter resource was {action} and is now at or below its low-stock threshold.

                Resource: {resource.Name}
                Category: {resource.ResourceCategory?.Name ?? "Unknown"}
                Current quantity: {resource.Quantity} {resource.Unit}
                Low-stock threshold: {resource.LowStockThreshold} {resource.Unit}

                Please review your shelter resources in PawConnect.
                """;

            var attachments = await TryCreatePdfAttachmentAsync(
                "LowStockResourceReport.pdf",
                () => pdfReportService.GenerateLowStockResourceReportAsync(resource.Id));

            var htmlBody = PawConnectEmailTemplate.BuildHtml(
                "Low stock warning",
                "Hello,",
                [$"A shelter resource was {action} and is now at or below its low-stock threshold.", "Please review your shelter resources in PawConnect."],
                details:
                [
                    new("Resource", resource.Name),
                    new("Category", resource.ResourceCategory?.Name ?? "Unknown"),
                    new("Current quantity", $"{resource.Quantity} {resource.Unit}"),
                    new("Low-stock threshold", $"{resource.LowStockThreshold} {resource.Unit}")
                ],
                hasAttachment: attachments.Count > 0);

            await emailService.SendEmailAsync(shelterEmail ?? string.Empty, $"Low stock warning: {resource.Name}", body, attachments, htmlBody);
            if (auditLogService is not null)
            {
                await auditLogService.LogSystemAsync(
                    AuditActions.ReportGenerated,
                    "ResourceStock",
                    resource.Id.ToString(),
                    $"Low-stock report was sent for resource {resource.Name}.",
                    additionalData: $"ShelterId={resource.ShelterId}");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Low-stock email notification failed for resource {ResourceId}.", resourceId);
        }
    }

    private async Task<List<EmailAttachment>> TryCreatePdfAttachmentAsync(string fileName, Func<Task<byte[]>> generatePdf)
    {
        try
        {
            var pdfBytes = await generatePdf();
            return
            [
                new EmailAttachment
                {
                    FileName = fileName,
                    ContentType = "application/pdf",
                    Content = pdfBytes
                }
            ];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PDF attachment {FileName} could not be generated.", fileName);
            return [];
        }
    }

    private Task LogAsync(string action, string entityName, string? entityId, string description, string? additionalData = null)
    {
        return auditLogService?.LogAsync(action, entityName, entityId, description, additionalData: additionalData) ?? Task.CompletedTask;
    }
}
