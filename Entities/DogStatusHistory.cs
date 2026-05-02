using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class DogStatusHistory
{
    public int Id { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }

    public DogStatus OldStatus { get; set; }

    public DogStatus NewStatus { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? ChangedByUserId { get; set; }

    public ApplicationUser? ChangedByUser { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
