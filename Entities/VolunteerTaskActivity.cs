using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class VolunteerTaskActivity
{
    public int Id { get; set; }

    public int VolunteerTaskId { get; set; }

    public VolunteerTask? VolunteerTask { get; set; }

    public string? ActorUserId { get; set; }

    public ApplicationUser? ActorUser { get; set; }

    public VolunteerTaskActivityType ActivityType { get; set; }

    [StringLength(1000)]
    public string? Message { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
