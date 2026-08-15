using System;

namespace CustomerService.Application.CustomerManagement.Dtos
{
    // What the API returns to callers. Kept separate from the Customer domain
    // entity so the API's response shape can evolve independently of the DB schema.
    public class CustomerDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
    }
}
