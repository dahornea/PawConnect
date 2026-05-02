using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class AdoptionRequest
{
    public int Id { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }

    [Required]
    public string AdopterId { get; set; } = string.Empty;

    public ApplicationUser? Adopter { get; set; }

    public AdoptionRequestStatus Status { get; set; } = AdoptionRequestStatus.Pending;

    [StringLength(1000)]
    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
