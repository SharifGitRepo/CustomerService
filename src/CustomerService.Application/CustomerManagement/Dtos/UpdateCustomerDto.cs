namespace CustomerService.Application.CustomerManagement.Dtos
{
    // Input shape for updating a customer. Fields are optional so callers can
    // send only the fields they want to change (matches the existing partial-update
    // behavior in CustomerEndpoints.cs).
    public class UpdateCustomerDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }
}
