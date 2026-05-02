using PawConnect.Data;

namespace PawConnect.Entities;

public class RecentlyViewedDog
{
    public int Id { get; set; }

    public string AdopterId { get; set; } = string.Empty;

    public ApplicationUser? Adopter { get; set; }

    public int DogId { get; set; }

    public Dog? Dog { get; set; }

    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}
