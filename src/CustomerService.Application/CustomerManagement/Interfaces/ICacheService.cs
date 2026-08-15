namespace CustomerService.Application.CustomerManagement.Interfaces
{
    // Abstraction over the caching provider, so the app doesn't care whether
    // it's talking to Redis (distributed, survives restarts, shareable across
    // instances) or an in-process memory cache (simpler, no extra infra, but
    // lost on restart and not shared if you ever scale to multiple instances).
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class;
        Task RemoveAsync(string key, CancellationToken ct = default);
    }
}
