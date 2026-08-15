using CustomerService.Application.CustomerManagement.Dtos;
using CustomerService.Application.CustomerManagement.Interfaces;

namespace CustomerService.Application.CustomerManagement.Services
{
    // Decorator pattern: wraps the real CustomerAppService and adds caching
    // around GetByIdAsync only. Deliberately NOT caching ListAsync — caching
    // an entire list would need invalidation on every single Create/Update/
    // Delete, which is a lot of extra complexity for a "list all" endpoint
    // that isn't the realistic hot path here. GET /customers/{id} is.
    //
    // Because this wraps ICustomerAppService rather than modifying
    // CustomerAppService directly, the original 21 unit tests for
    // CustomerAppService's business logic remain completely untouched.
    public class CachedCustomerAppService : ICustomerAppService
    {
        private readonly ICustomerAppService _inner;
        private readonly ICacheService _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public CachedCustomerAppService(ICustomerAppService inner, ICacheService cache)
        {
            _inner = inner;
            _cache = cache;
        }

        private static string CacheKey(Guid id) => $"customer:{id}";

        // Not cached — see class-level note above.
        public Task<List<CustomerDto>> ListAsync(CancellationToken ct = default) => _inner.ListAsync(ct);

        public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var cached = await _cache.GetAsync<CustomerDto>(CacheKey(id), ct);
            if (cached is not null)
                return cached;

            var result = await _inner.GetByIdAsync(id, ct);
            if (result is not null)
                await _cache.SetAsync(CacheKey(id), result, CacheDuration, ct);

            return result;
        }

        // Not cached — nothing to invalidate for a brand-new record.
        public Task<CustomerDto> CreateAsync(CreateCustomerDto input, CancellationToken ct = default) =>
            _inner.CreateAsync(input, ct);

        public async Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerDto input, CancellationToken ct = default)
        {
            var result = await _inner.UpdateAsync(id, input, ct);
            // Reached only if UpdateAsync didn't throw. A successful update means
            // the cached value is now stale, so clear it. A null result (id not
            // found) means GetByIdAsync never cached anything for this id in the
            // first place, so this is a harmless no-op. If validation fails,
            // CustomerValidationException propagates up before this line runs —
            // nothing changed in the DB, so the existing cache entry is still
            // accurate and correctly left untouched.
            await _cache.RemoveAsync(CacheKey(id), ct);
            return result;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var deleted = await _inner.DeleteAsync(id, ct);
            if (deleted)
                await _cache.RemoveAsync(CacheKey(id), ct);
            return deleted;
        }
    }
}
