using CustomerService.Application.CustomerManagement.Interfaces;
using CustomerService.Application.CustomerManagement.Services;
using CustomerService.Infrastructure.Caching;
using CustomerService.Infrastructure.Data;
using CustomerService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using CustomerService.API.Endpoints;
using Microsoft.AspNetCore.Builder;
using Azure.Monitor.OpenTelemetry.AspNetCore;  // Required for WebApplication extensions


var builder = WebApplication.CreateBuilder(args);


//Add DbContext
builder.Services.AddDbContext<CustomerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CustDb"),
    sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
    }));


// Add repository
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();

// Add the real Application-layer service (unwrapped, un-cached)
builder.Services.AddScoped<CustomerAppService>();

// --- Caching ---
// "Caching:Provider" in appsettings.json selects "Redis" or "Memory".
// Defaults to "Memory" so the app runs out of the box with zero extra
// infrastructure. Switch to "Redis" once the redis service in
// docker-compose.yml is running, to demonstrate the distributed-cache path.
var cacheProvider = builder.Configuration.GetValue<string>("Caching:Provider") ?? "Memory";

if (string.Equals(cacheProvider, "Redis", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
    });
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
}
else
{
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<ICacheService, MemoryCacheService>();
}

// Endpoints depend on ICustomerAppService, which is the CACHED decorator
// wrapping the real CustomerAppService. This keeps the caching concern
// entirely out of CustomerAppService itself and its existing test suite.
builder.Services.AddScoped<ICustomerAppService>(sp =>
    new CachedCustomerAppService(
        sp.GetRequiredService<CustomerAppService>(),
        sp.GetRequiredService<ICacheService>()));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenTelemetry().UseAzureMonitor();

var app = builder.Build();

// Enable Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapCustomerEndpoints();

app.Run();
