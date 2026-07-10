using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class ShelterSimulationScenario
{
    public int Id { get; set; }

    [Required, StringLength(140)]
    public string Name { get; set; } = string.Empty;

    [StringLength(800)]
    public string? Description { get; set; }

    [Required, StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser? CreatedByUser { get; set; }

    public int? ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    public SimulationScopeType ScopeType { get; set; }

    [Range(1, 90)]
    public int HorizonDays { get; set; } = 14;

    public SimulationScenarioStatus Status { get; set; } = SimulationScenarioStatus.Draft;

    [Required]
    public string AssumptionsJson { get; set; } = "[]";

    public bool IsPinned { get; set; }

    public bool IsTemplate { get; set; }

    public DateTime? LastRunAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ShelterSimulationRun> Runs { get; set; } = [];
}
