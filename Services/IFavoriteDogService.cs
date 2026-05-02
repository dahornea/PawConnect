using PawConnect.Entities;

namespace PawConnect.Services;

public interface IFavoriteDogService
{
    Task<List<FavoriteDog>> GetFavoritesForUserAsync(string userId);
}
