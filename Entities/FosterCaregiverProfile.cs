using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class FosterCaregiverProfile
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    [Required, StringLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(40)]
    public string? PhoneNumber { get; set; }

    [StringLength(250)]
    public string? AddressSummary { get; set; }

    public int? PreferredShelterId { get; set; }

    public Shelter? PreferredShelter { get; set; }

    [StringLength(1000)]
    public string? ExperienceNotes { get; set; }

    [StringLength(1000)]
    public string? HomeEnvironmentNotes { get; set; }

    [Range(1, 20)]
    public int Capacity { get; set; } = 1;

    [Range(0, 20)]
    public int ActivePlacementCount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<FosterPlacement> Placements { get; set; } = new List<FosterPlacement>();
}
