using PawConnect.Entities;

namespace PawConnect.Services;

public interface IFavoriteDogService
{
    Task<List<FavoriteDog>> GetAllAsync();

    Task<FavoriteDog?> GetByIdAsync(int id);

    Task CreateAsync(FavoriteDog favoriteDog);

    Task UpdateAsync(FavoriteDog favoriteDog);

    Task DeleteAsync(int id);

    Task<List<FavoriteDog>> GetFavoritesForUserAsync(string userId);
}
