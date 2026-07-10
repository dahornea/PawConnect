using System.ComponentModel.DataAnnotations;
using PawConnect.Data;

namespace PawConnect.Entities;

public class ShelterSimulationRun
{
    public int Id { get; set; }

    public int? ScenarioId { get; set; }

    public ShelterSimulationScenario? Scenario { get; set; }

    [Required, StringLength(450)]
    public string RunByUserId { get; set; } = string.Empty;

    public ApplicationUser? RunByUser { get; set; }

    public int? ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    [Range(1, 90)]
    public int HorizonDays { get; set; }

    [Required]
    public string BaselineSnapshotJson { get; set; } = "{}";

    [Required]
    public string AssumptionsSnapshotJson { get; set; } = "[]";

    [Required]
    public string ResultSummaryJson { get; set; } = "{}";

    [Required]
    public string RiskDeltaJson { get; set; } = "[]";

    [Required]
    public string CapacityDeltaJson { get; set; } = "{}";

    [Required]
    public string RecommendationSummaryJson { get; set; } = "[]";

    [Required, StringLength(40)]
    public string EngineVersion { get; set; } = "1.0";

    public DateTime StartedAtUtc { get; set; }

    public DateTime CompletedAtUtc { get; set; }

    public long DurationMilliseconds { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
