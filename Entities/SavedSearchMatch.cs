using System.ComponentModel.DataAnnotations;

namespace PawConnect.Entities;

public class SavedSearchMatch
{
    public int Id { get; set; }

    public int SavedDogSearchId { get; set; }

    public SavedDogSearch? SavedDogSearch { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }

    public int MatchScore { get; set; }

    [StringLength(2000)]
    public string? MatchReasonsJson { get; set; }

    public SavedSearchMatchStatus Status { get; set; } = SavedSearchMatchStatus.New;

    public DateTime FirstMatchedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastMatchedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? SeenAtUtc { get; set; }

    public DateTime? DismissedAtUtc { get; set; }

    public DateTime? NotificationSentAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
