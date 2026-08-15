using CustomerService.Application.CustomerManagement.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CustomerService.Infrastructure.Caching
{
    // Backed by IMemoryCache — in-process only, no extra infrastructure needed.
    // Good default for local dev without a Redis container running; not shared
    // across instances if the app is ever scaled horizontally.
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

        public MemoryCacheService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        {
            _cache.TryGetValue(key, out T? value);
            return Task.FromResult(value);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        {
            _cache.Set(key, value, expiration ?? DefaultExpiration);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
