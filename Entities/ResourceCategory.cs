using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class ResourceCategory
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    public ICollection<ResourceStock> ResourceStocks { get; set; } = new List<ResourceStock>();
}
