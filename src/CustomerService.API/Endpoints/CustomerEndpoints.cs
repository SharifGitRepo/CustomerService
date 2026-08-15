using CustomerService.Application.CustomerManagement.Dtos;
using CustomerService.Application.CustomerManagement.Exceptions;
using CustomerService.Application.CustomerManagement.Interfaces;

namespace CustomerService.API.Endpoints
{
    public static class CustomerEndpoints
    {
        // This method will be called from Program.cs
        public static void MapCustomerEndpoints(this WebApplication app)
        {
            // Root endpoint
            app.MapGet("/", () => "Customer Service API is running");

            // Get all customers
            app.MapGet("/customers", async (ICustomerAppService service, CancellationToken ct) =>
                Results.Ok(await service.ListAsync(ct)));

            // Get a customer by ID
            app.MapGet("/customers/{id}", async (Guid id, ICustomerAppService service, CancellationToken ct) =>
            {
                var customer = await service.GetByIdAsync(id, ct);
                return customer is not null ? Results.Ok(customer) : Results.NotFound();
            });

            // Create a new customer
            app.MapPost("/customers", async (CreateCustomerDto input, ICustomerAppService service, CancellationToken ct) =>
            {
                try
                {
                    var created = await service.CreateAsync(input, ct);
                    return Results.Created($"/customers/{created.Id}", created);
                }
                catch (CustomerValidationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // Update a customer
            app.MapPut("/customers/{id}", async (Guid id, UpdateCustomerDto input, ICustomerAppService service, CancellationToken ct) =>
            {
                try
                {
                    var updated = await service.UpdateAsync(id, input, ct);
                    return updated is not null ? Results.Ok(updated) : Results.NotFound();
                }
                catch (CustomerValidationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            // Delete a customer
            app.MapDelete("/customers/{id}", async (Guid id, ICustomerAppService service, CancellationToken ct) =>
            {
                var deleted = await service.DeleteAsync(id, ct);
                return deleted ? Results.NoContent() : Results.NotFound();
            });
        }
    }
}
