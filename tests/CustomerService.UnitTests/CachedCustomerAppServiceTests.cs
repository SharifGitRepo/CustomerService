using CustomerService.Application.CustomerManagement.Dtos;
using CustomerService.Application.CustomerManagement.Interfaces;
using CustomerService.Application.CustomerManagement.Services;
using Moq;
using Xunit;

namespace CustomerService.UnitTests.CustomerManagement.Services
{
    // Tests the caching DECORATOR in isolation — both the inner ICustomerAppService
    // and ICacheService are mocked. This never touches CustomerAppService's real
    // logic or the 21 existing tests for it; it only verifies the decorator's own
    // job: check cache, fall through on miss, populate on miss, invalidate on write.
    public class CachedCustomerAppServiceTests
    {
        private readonly Mock<ICustomerAppService> _inner;
        private readonly Mock<ICacheService> _cache;
        private readonly CachedCustomerAppService _sut;

        public CachedCustomerAppServiceTests()
        {
            _inner = new Mock<ICustomerAppService>();
            _cache = new Mock<ICacheService>();
            _sut = new CachedCustomerAppService(_inner.Object, _cache.Object);
        }

        private static CustomerDto MakeDto(Guid id) => new() { Id = id, Name = "Alice", Email = "alice@example.com" };

        [Fact]
        public async Task GetByIdAsync_CacheHit_ReturnsCachedValue_AndNeverCallsInner()
        {
            var id = Guid.NewGuid();
            var cached = MakeDto(id);
            _cache.Setup(c => c.GetAsync<CustomerDto>($"customer:{id}", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(cached);

            var result = await _sut.GetByIdAsync(id);

            Assert.Equal(cached, result);
            _inner.Verify(i => i.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetByIdAsync_CacheMiss_CallsInnerAndPopulatesCache()
        {
            var id = Guid.NewGuid();
            var fromInner = MakeDto(id);
            _cache.Setup(c => c.GetAsync<CustomerDto>($"customer:{id}", It.IsAny<CancellationToken>()))
                  .ReturnsAsync((CustomerDto?)null);
            _inner.Setup(i => i.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(fromInner);

            var result = await _sut.GetByIdAsync(id);

            Assert.Equal(fromInner, result);
            _cache.Verify(c => c.SetAsync($"customer:{id}", fromInner, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_CacheMissAndInnerReturnsNull_DoesNotCacheNull()
        {
            var id = Guid.NewGuid();
            _cache.Setup(c => c.GetAsync<CustomerDto>($"customer:{id}", It.IsAny<CancellationToken>()))
                  .ReturnsAsync((CustomerDto?)null);
            _inner.Setup(i => i.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((CustomerDto?)null);

            var result = await _sut.GetByIdAsync(id);

            Assert.Null(result);
            _cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<CustomerDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_SuccessfulUpdate_InvalidatesCacheForThatId()
        {
            var id = Guid.NewGuid();
            var updated = MakeDto(id);
            _inner.Setup(i => i.UpdateAsync(id, It.IsAny<UpdateCustomerDto>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(updated);

            await _sut.UpdateAsync(id, new UpdateCustomerDto { Name = "New Name" });

            _cache.Verify(c => c.RemoveAsync($"customer:{id}", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ExistingId_InvalidatesCache()
        {
            var id = Guid.NewGuid();
            _inner.Setup(i => i.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await _sut.DeleteAsync(id);

            Assert.True(result);
            _cache.Verify(c => c.RemoveAsync($"customer:{id}", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_NonExistentId_ReturnsFalse_AndDoesNotTouchCache()
        {
            var id = Guid.NewGuid();
            _inner.Setup(i => i.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var result = await _sut.DeleteAsync(id);

            Assert.False(result);
            _cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ListAsync_PassesThroughDirectly_WithoutTouchingCache()
        {
            var expected = new List<CustomerDto> { MakeDto(Guid.NewGuid()) };
            _inner.Setup(i => i.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

            var result = await _sut.ListAsync();

            Assert.Equal(expected, result);
            _cache.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CreateAsync_PassesThroughDirectly_WithoutTouchingCache()
        {
            var created = MakeDto(Guid.NewGuid());
            _inner.Setup(i => i.CreateAsync(It.IsAny<CreateCustomerDto>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(created);

            var result = await _sut.CreateAsync(new CreateCustomerDto { Name = "Alice", Email = "alice@example.com" });

            Assert.Equal(created, result);
            _cache.VerifyNoOtherCalls();
        }
    }
}

