using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class FoodType
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    public ICollection<ResourceStock> ResourceStocks { get; set; } = new List<ResourceStock>();

    public ICollection<Dog> Dogs { get; set; } = new List<Dog>();
}
