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

    Task<int> GetFavoriteCountForUserAsync(string adopterId);

    Task<List<FavoriteDog>> GetRecentFavoritesForUserAsync(string adopterId, int count);

    Task<HashSet<int>> GetFavoriteDogIdsForUserAsync(string adopterId);

    Task<bool> IsFavoriteAsync(string adopterId, int dogId);

    Task AddFavoriteAsync(string adopterId, int dogId);

    Task RemoveFavoriteAsync(string adopterId, int dogId);
}
