using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace PawConnect.Services.Caching;

public class LocalCacheService(IMemoryCache memoryCache) : ILocalCacheService
{
    private readonly ConcurrentDictionary<string, byte> cacheKeys = new(StringComparer.Ordinal);

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan absoluteExpirationRelativeToNow)
    {
        if (memoryCache.TryGetValue<T>(key, out var cachedValue) && cachedValue is not null)
        {
            return cachedValue;
        }

        var value = await factory();
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow
        };

        options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string textKey)
            {
                cacheKeys.TryRemove(textKey, out _);
            }
        });

        memoryCache.Set(key, value, options);
        cacheKeys[key] = 0;
        return value;
    }

    public void Remove(string key)
    {
        memoryCache.Remove(key);
        cacheKeys.TryRemove(key, out _);
    }

    public void RemoveByPrefix(string prefix)
    {
        foreach (var key in cacheKeys.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            Remove(key);
        }
    }
}
