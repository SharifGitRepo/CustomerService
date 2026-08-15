using CustomerService.Application.CustomerManagement.Dtos;
using CustomerService.Application.CustomerManagement.Exceptions;
using CustomerService.Application.CustomerManagement.Interfaces;
using CustomerService.Application.CustomerManagement.Services;
using CustomerService.Domain.Entities;
using Moq;
using Xunit;

namespace CustomerService.UnitTests.CustomerManagement.Services
{
    // Tests target CustomerAppService in isolation — ICustomerRepository is mocked,
    // so no database is involved. Every test maps to an actual branch in
    // CustomerAppService.cs, not a generic CRUD checklist.
    public class CustomerAppServiceTests
    {
        private readonly Mock<ICustomerRepository> _repo;
        private readonly CustomerAppService _sut; // "system under test"

        public CustomerAppServiceTests()
        {
            _repo = new Mock<ICustomerRepository>();
            _sut = new CustomerAppService(_repo.Object);
        }

        private static Customer MakeCustomer(string name = "Imdadul Sharif", string email = "sharifimdadul25@gmail.com") =>
            new() { Id = Guid.NewGuid(), Name = name, Email = email };

        // ---------- ListAsync ----------

        [Fact]
        public async Task ListAsync_RepositoryReturnsEmptyList_ReturnsEmptyList()
        {
            _repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Customer>());

            var result = await _sut.ListAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task ListAsync_RepositoryReturnsCustomers_MapsAllFieldsCorrectly()
        {
            var customer = MakeCustomer("Kabin", "kabin@example.com");
            _repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Customer> { customer });

            var result = await _sut.ListAsync();

            var dto = Assert.Single(result);
            Assert.Equal(customer.Id, dto.Id);
            Assert.Equal(customer.Name, dto.Name);
            Assert.Equal(customer.Email, dto.Email);
        }

        // ---------- GetByIdAsync ----------

        [Fact]
        public async Task GetByIdAsync_ExistingId_ReturnsMatchingDto()
        {
            var customer = MakeCustomer();
            _repo.Setup(r => r.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(customer);

            var result = await _sut.GetByIdAsync(customer.Id);

            Assert.NotNull(result);
            Assert.Equal(customer.Id, result!.Id);
        }

        [Fact]
        public async Task GetByIdAsync_NonExistentId_ReturnsNull()
        {
            _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Customer?)null);

            var result = await _sut.GetByIdAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        // ---------- CreateAsync ----------

        [Fact]
        public async Task CreateAsync_ValidInput_CallsAddAsyncOnceAndReturnsDtoWithNewId()
        {
            var input = new CreateCustomerDto { Name = "Ardia", Email = "ardia@example.com" };

            var result = await _sut.CreateAsync(input);

            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal("Ardia", result.Name);
            Assert.Equal("ardia@example.com", result.Email);
            _repo.Verify(r => r.AddAsync(
                It.Is<Customer>(c => c.Name == "Ardia" && c.Email == "ardia@example.com"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAsync_MissingName_ThrowsValidationException_AndNeverCallsAddAsync(string? name)
        {
            var input = new CreateCustomerDto { Name = name, Email = "valid@example.com" };

            var ex = await Assert.ThrowsAsync<CustomerValidationException>(() => _sut.CreateAsync(input));

            Assert.Equal("Customer name is required.", ex.Message);
            _repo.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("no-at-symbol.com")]
        [InlineData("no-dot@example")]
        public async Task CreateAsync_InvalidEmail_ThrowsValidationException_AndNeverCallsAddAsync(string? email)
        {
            var input = new CreateCustomerDto { Name = "Valid Name", Email = email };

            var ex = await Assert.ThrowsAsync<CustomerValidationException>(() => _sut.CreateAsync(input));

            Assert.Equal("A valid customer email is required.", ex.Message);
            _repo.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_EmailWithDoubleAtSymbol_CurrentlyPassesValidation()
        {
            // Documents an ACTUAL gap in the current validation logic: it only checks
            // Contains('@') and Contains('.') independently, so a malformed address
            // like "a@@b.com" is accepted today. This test locks in current behavior
            // so a future change to Validate() doesn't silently alter it unnoticed.
            // If stricter email validation is added later, this test should be
            // updated to expect a CustomerValidationException instead.
            var input = new CreateCustomerDto { Name = "Valid Name", Email = "a@@b.com" };

            var result = await _sut.CreateAsync(input);

            Assert.Equal("a@@b.com", result.Email);
        }

        // ---------- UpdateAsync ----------

        [Fact]
        public async Task UpdateAsync_NonExistentId_ReturnsNull_AndNeverCallsUpdateAsync()
        {
            _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Customer?)null);

            var result = await _sut.UpdateAsync(Guid.NewGuid(), new UpdateCustomerDto { Name = "New" });

            Assert.Null(result);
            _repo.Verify(r => r.UpdateAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_BothFieldsProvided_UpdatesBothFields()
        {
            var existing = MakeCustomer("Old Name", "old@example.com");
            _repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

            var result = await _sut.UpdateAsync(existing.Id,
                new UpdateCustomerDto { Name = "New Name", Email = "new@example.com" });

            Assert.Equal("New Name", result!.Name);
            Assert.Equal("new@example.com", result.Email);
            _repo.Verify(r => r.UpdateAsync(
                It.Is<Customer>(c => c.Name == "New Name" && c.Email == "new@example.com"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_OnlyNameProvided_PreservesExistingEmail()
        {
            // This is the actual partial-update contract: fields left null in the
            // DTO must not overwrite existing data.
            var existing = MakeCustomer("Old Name", "keep-this@example.com");
            _repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

            var result = await _sut.UpdateAsync(existing.Id, new UpdateCustomerDto { Name = "New Name", Email = null });

            Assert.Equal("New Name", result!.Name);
            Assert.Equal("keep-this@example.com", result.Email);
        }

        [Fact]
        public async Task UpdateAsync_OnlyEmailProvided_PreservesExistingName()
        {
            var existing = MakeCustomer("Keep This Name", "old@example.com");
            _repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

            var result = await _sut.UpdateAsync(existing.Id, new UpdateCustomerDto { Name = null, Email = "new@example.com" });

            Assert.Equal("Keep This Name", result!.Name);
            Assert.Equal("new@example.com", result.Email);
        }

        [Fact]
        public async Task UpdateAsync_ExplicitEmptyStringName_IsTreatedAsAttemptToClear_AndFailsValidation()
        {
            // Subtle but important: the merge logic is `input.Name ?? existing.Name`,
            // which only falls back to the existing value when the DTO field is
            // literally null. An explicit "" is NOT null, so it overrides the
            // existing name — and should then correctly fail validation, rather
            // than being silently treated the same as "no change requested".
            var existing = MakeCustomer("Old Name", "old@example.com");
            _repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

            var ex = await Assert.ThrowsAsync<CustomerValidationException>(
                () => _sut.UpdateAsync(existing.Id, new UpdateCustomerDto { Name = "", Email = null }));

            Assert.Equal("Customer name is required.", ex.Message);
            _repo.Verify(r => r.UpdateAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_MergedEmailInvalid_ThrowsAndNeverCallsUpdateAsync()
        {
            var existing = MakeCustomer("Old Name", "old@example.com");
            _repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

            await Assert.ThrowsAsync<CustomerValidationException>(
                () => _sut.UpdateAsync(existing.Id, new UpdateCustomerDto { Name = null, Email = "not-an-email" }));

            _repo.Verify(r => r.UpdateAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---------- DeleteAsync ----------

        [Fact]
        public async Task DeleteAsync_NonExistentId_ReturnsFalse_AndNeverCallsDeleteAsync()
        {
            _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Customer?)null);

            var result = await _sut.DeleteAsync(Guid.NewGuid());

            Assert.False(result);
            _repo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_ExistingId_ReturnsTrue_AndCallsDeleteAsyncOnceWithCorrectId()
        {
            var existing = MakeCustomer();
            _repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

            var result = await _sut.DeleteAsync(existing.Id);

            Assert.True(result);
            _repo.Verify(r => r.DeleteAsync(existing.Id, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
