using CustomerService.Domain.Entities;

namespace CustomerService.Application.CustomerManagement.Interfaces
{
    public interface ICustomerRepository
    {
        Task<List<Customer>> ListAsync(CancellationToken ct = default);
        Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task AddAsync(Customer customer, CancellationToken ct = default);
        Task UpdateAsync(Customer customer, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
