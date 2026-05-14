using SubscriptionManager.Api.Repositories;
using SubscriptionManager.Api.Services;

namespace SubscriptionManager.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Repositories — singleton so in-memory data persists
        services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
        services.AddSingleton<ISubscriptionRepository, InMemorySubscriptionRepository>();

        // Services — scoped
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        return services;
    }
}
