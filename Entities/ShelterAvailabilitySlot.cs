using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class ShelterAvailabilitySlot
{
    public int Id { get; set; }

    public int ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public bool IsBooked { get; set; }

    public int? BookedAdoptionRequestId { get; set; }

    public AdoptionRequest? BookedAdoptionRequest { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsCancelled { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelledByUserId { get; set; }

    public ApplicationUser? CancelledByUser { get; set; }
}
