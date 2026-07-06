using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class DogTransferRequest
{
    public int Id { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }

    public int SourceShelterId { get; set; }

    public Shelter? SourceShelter { get; set; }

    public int DestinationShelterId { get; set; }

    public Shelter? DestinationShelter { get; set; }

    [Required]
    public string RequestedByUserId { get; set; } = string.Empty;

    public ApplicationUser? RequestedByUser { get; set; }

    public string? RespondedByUserId { get; set; }

    public ApplicationUser? RespondedByUser { get; set; }

    public string? CompletedByUserId { get; set; }

    public ApplicationUser? CompletedByUser { get; set; }

    public DogTransferStatus Status { get; set; } = DogTransferStatus.Pending;

    public DogTransferPriority Priority { get; set; } = DogTransferPriority.Normal;

    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? SourceShelterNotes { get; set; }

    [StringLength(1000)]
    public string? DestinationShelterResponseNotes { get; set; }

    [StringLength(1000)]
    public string? AdminNotes { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? RespondedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
