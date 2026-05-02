using Microsoft.AspNetCore.Identity;
using PawConnect.Entities;

namespace PawConnect.Data;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }

    public ICollection<FavoriteDog> FavoriteDogs { get; set; } = new List<FavoriteDog>();

    public ICollection<RecentlyViewedDog> RecentlyViewedDogs { get; set; } = new List<RecentlyViewedDog>();

    public ICollection<AdoptionRequest> AdoptionRequests { get; set; } = new List<AdoptionRequest>();

    public ICollection<DogStatusHistory> DogStatusHistories { get; set; } = new List<DogStatusHistory>();

    public Shelter? Shelter { get; set; }

    public AdopterProfile? AdopterProfile { get; set; }
}

