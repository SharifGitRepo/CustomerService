using System;

namespace CustomerService.Application.CustomerManagement.Exceptions
{
    // Thrown by CustomerAppService when a business/validation rule fails
    // (e.g. missing name, malformed email). Kept intentionally simple —
    // no validation framework, just a typed exception the API layer can
    // catch and translate into a 400 Bad Request.
    public class CustomerValidationException : Exception
    {
        public CustomerValidationException(string message) : base(message) { }
    }
}
