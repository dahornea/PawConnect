using PawConnect.Entities;

namespace PawConnect.Services;

public interface IRecentlyViewedDogService
{
    Task TrackViewAsync(string adopterId, int dogId);

    Task<List<RecentlyViewedDog>> GetRecentlyViewedDogsAsync(string adopterId, int count);

    Task ClearRecentlyViewedAsync(string adopterId);
}
