using CustomerService.Application.CustomerManagement.Dtos;

namespace CustomerService.Application.CustomerManagement.Interfaces
{
    // The API layer talks to this, never directly to ICustomerRepository.
    // This is the seam where validation, mapping, and (later) authorization
    // rules live — e.g. "only Admin role can call DeleteAsync".
    public interface ICustomerAppService
    {
        Task<List<CustomerDto>> ListAsync(CancellationToken ct = default);
        Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<CustomerDto> CreateAsync(CreateCustomerDto input, CancellationToken ct = default);
        Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerDto input, CancellationToken ct = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
