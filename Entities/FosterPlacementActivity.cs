using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class FosterPlacementActivity
{
    public int Id { get; set; }

    public int FosterPlacementId { get; set; }

    public FosterPlacement? FosterPlacement { get; set; }

    public string? ActorUserId { get; set; }

    public ApplicationUser? ActorUser { get; set; }

    public FosterPlacementActivityType ActivityType { get; set; }

    [Required, StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
