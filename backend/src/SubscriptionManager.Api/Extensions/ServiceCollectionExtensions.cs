using Microsoft.EntityFrameworkCore;
using SubscriptionManager.Api.Data;
using SubscriptionManager.Api.Repositories;
using SubscriptionManager.Api.Services;

namespace SubscriptionManager.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. Set it in " +
                "appsettings.json or via the ConnectionStrings__DefaultConnection " +
                "environment variable.");

        // Scoped by default, which is what we want — a DbContext is not thread-safe
        // and is designed to live for one request.
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        // Repositories — Scoped.
        //
        // They were Singleton, for exactly one reason: to keep in-memory state alive
        // between requests. External storage removes that reason. Leaving them
        // Singleton would be worse than pointless — a singleton capturing a scoped
        // DbContext is a lifetime violation that shows up as concurrency corruption
        // under load rather than as an error at startup.
        services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<ISubscriptionRepository, EfSubscriptionRepository>();

        // Services — Scoped, unchanged.
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        return services;
    }
}
