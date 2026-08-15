namespace CustomerService.Application.CustomerManagement.Dtos
{
    // Input shape for creating a customer. No Id — the server assigns it.
    public class CreateCustomerDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }
}
