namespace PawConnect.Services.Caching;

public interface ILocalCacheService
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan absoluteExpirationRelativeToNow);

    void Remove(string key);

    void RemoveByPrefix(string prefix);
}
