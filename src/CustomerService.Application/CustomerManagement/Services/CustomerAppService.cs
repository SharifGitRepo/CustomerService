using CustomerService.Application.CustomerManagement.Dtos;
using CustomerService.Application.CustomerManagement.Exceptions;
using CustomerService.Application.CustomerManagement.Interfaces;
using CustomerService.Domain.Entities;

namespace CustomerService.Application.CustomerManagement.Services
{
    // Named CustomerAppService (not CustomerService) deliberately — the original
    // roadmap flagged a naming collision with the API project/assembly name.
    // This class owns entity<->DTO mapping and basic validation. It stays thin:
    // no MediatR, no CQRS handlers — this is a small CRUD service and doesn't
    // need that machinery.
    public class CustomerAppService : ICustomerAppService
    {
        private readonly ICustomerRepository _repository;

        public CustomerAppService(ICustomerRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<CustomerDto>> ListAsync(CancellationToken ct = default)
        {
            var customers = await _repository.ListAsync(ct);
            return customers.Select(ToDto).ToList();
        }

        public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var customer = await _repository.GetByIdAsync(id, ct);
            return customer is null ? null : ToDto(customer);
        }

        public async Task<CustomerDto> CreateAsync(CreateCustomerDto input, CancellationToken ct = default)
        {
            Validate(input.Name, input.Email);

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = input.Name,
                Email = input.Email
            };

            await _repository.AddAsync(customer, ct);
            return ToDto(customer);
        }

        public async Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerDto input, CancellationToken ct = default)
        {
            var existing = await _repository.GetByIdAsync(id, ct);
            if (existing is null)
                return null;

            // Partial update: only overwrite fields the caller actually provided
            // (preserves the existing behavior from CustomerEndpoints.cs).
            var newName = input.Name ?? existing.Name;
            var newEmail = input.Email ?? existing.Email;

            Validate(newName, newEmail);

            existing.Name = newName;
            existing.Email = newEmail;

            await _repository.UpdateAsync(existing, ct);
            return ToDto(existing);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var existing = await _repository.GetByIdAsync(id, ct);
            if (existing is null)
                return false;

            await _repository.DeleteAsync(id, ct);
            return true;
        }

        // Deliberately simple: no FluentValidation/DataAnnotations framework for
        // a two-field entity. Swap this out if validation rules grow more complex.
        private static void Validate(string? name, string? email)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new CustomerValidationException("Customer name is required.");

            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
                throw new CustomerValidationException("A valid customer email is required.");
        }

        private static CustomerDto ToDto(Customer customer) => new()
        {
            Id = customer.Id,
            Name = customer.Name,
            Email = customer.Email
        };
    }
}

