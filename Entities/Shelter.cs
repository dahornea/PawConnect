using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class Shelter
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required, StringLength(160)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string City { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? PhoneNumber { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    public TimeSpan? VisitStartTime { get; set; } = new(10, 0, 0);

    public TimeSpan? VisitEndTime { get; set; } = new(17, 0, 0);

    public bool VisitsAllowedMonday { get; set; } = true;

    public bool VisitsAllowedTuesday { get; set; } = true;

    public bool VisitsAllowedWednesday { get; set; } = true;

    public bool VisitsAllowedThursday { get; set; } = true;

    public bool VisitsAllowedFriday { get; set; } = true;

    public bool VisitsAllowedSaturday { get; set; }

    public bool VisitsAllowedSunday { get; set; }

    public string? ApplicationUserId { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }

    public ICollection<Dog> Dogs { get; set; } = new List<Dog>();

    public ICollection<ResourceStock> ResourceStocks { get; set; } = new List<ResourceStock>();
}
