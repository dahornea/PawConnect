using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class Shelter
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Address { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? PhoneNumber { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public string? OwnerUserId { get; set; }

    public ApplicationUser? OwnerUser { get; set; }

    public ICollection<Dog> Dogs { get; set; } = new List<Dog>();

    public ICollection<ResourceStock> ResourceStocks { get; set; } = new List<ResourceStock>();
}
