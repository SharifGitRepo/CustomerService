using CustomerService.Application.CustomerManagement.Interfaces;
using CustomerService.Domain.Entities;
using CustomerService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Infrastructure.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly CustomerDbContext _db;

        public CustomerRepository(CustomerDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(Customer customer, CancellationToken ct = default)
        {
            if (customer.Id == Guid.Empty)
                customer.Id = Guid.NewGuid();

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _db.Customers.FindAsync(new object[] { id }, ct);
        }

        public async Task<List<Customer>> ListAsync(CancellationToken ct = default)
        {
            return await _db.Customers.ToListAsync(ct);
        }

        public async Task UpdateAsync(Customer customer, CancellationToken ct = default)
        {
            _db.Customers.Update(customer);
            await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var customer = await _db.Customers.FindAsync(new object[] { id }, ct);
            if (customer is not null)
            {
                _db.Customers.Remove(customer);
                await _db.SaveChangesAsync(ct);
            }
        }
    }
}
