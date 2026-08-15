using System.Text.Json;
using CustomerService.Application.CustomerManagement.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace CustomerService.Infrastructure.Caching
{
    // Backed by IDistributedCache configured for Redis (via AddStackExchangeRedisCache
    // in Program.cs). Values are JSON-serialized since Redis only stores strings/bytes.

    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

        public RedisCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        {
            var json = await _cache.GetStringAsync(key, ct);
            return json is null ? null : JsonSerializer.Deserialize<T>(json);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        {
            var json = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
            };
            await _cache.SetStringAsync(key, json, options, ct);
        }

        public Task RemoveAsync(string key, CancellationToken ct = default) => _cache.RemoveAsync(key, ct);
    }
}
