using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class ResourceStock
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100000)]
    public int Quantity { get; set; }

    [Required, StringLength(30)]
    public string Unit { get; set; } = string.Empty;

    [Range(0, 100000)]
    public int LowStockThreshold { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    public int ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    public int ResourceCategoryId { get; set; }

    public ResourceCategory? ResourceCategory { get; set; }

    public int? FoodTypeId { get; set; }

    public FoodType? FoodType { get; set; }
}
