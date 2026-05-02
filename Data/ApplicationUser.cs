using Microsoft.AspNetCore.Identity;
using PawConnect.Entities;

namespace PawConnect.Data;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }

    public ICollection<FavoriteDog> FavoriteDogs { get; set; } = new List<FavoriteDog>();

    public ICollection<AdoptionRequest> AdoptionRequests { get; set; } = new List<AdoptionRequest>();

    public Shelter? Shelter { get; set; }
}

