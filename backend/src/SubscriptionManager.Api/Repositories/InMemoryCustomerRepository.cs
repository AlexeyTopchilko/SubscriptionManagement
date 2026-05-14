using System.Collections.Concurrent;
using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.Repositories;

public class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly ConcurrentDictionary<Guid, Customer> _customers = new();

    public InMemoryCustomerRepository()
    {
        SeedData();
    }

    public Task<IEnumerable<Customer>> GetAllAsync()
    {
        var customers = _customers.Values.OrderBy(c => c.CreatedAt).ToList();
        return Task.FromResult<IEnumerable<Customer>>(customers);
    }

    public Task<Customer?> GetByIdAsync(Guid id)
    {
        _customers.TryGetValue(id, out var customer);
        return Task.FromResult(customer);
    }

    public Task<Customer?> GetByEmailAsync(string email)
    {
        var customer = _customers.Values.FirstOrDefault(c =>
            c.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(customer);
    }

    public Task<Customer> CreateAsync(Customer customer)
    {
        _customers[customer.Id] = customer;
        return Task.FromResult(customer);
    }

    public Task<Customer?> UpdateAsync(Customer customer)
    {
        if (!_customers.ContainsKey(customer.Id))
            return Task.FromResult<Customer?>(null);

        _customers[customer.Id] = customer;
        return Task.FromResult<Customer?>(customer);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var removed = _customers.TryRemove(id, out _);
        return Task.FromResult(removed);
    }

    public Task<bool> ExistsAsync(Guid id)
    {
        return Task.FromResult(_customers.ContainsKey(id));
    }

    private void SeedData()
    {
        var customer1Id = Guid.NewGuid();
        var customer2Id = Guid.NewGuid();
        var customer3Id = Guid.NewGuid();

        var customer1 = new Customer
        {
            Id = customer1Id,
            Name = "Alex Gromov",
            Email = "alex@company.com",
            Company = "Technologies LLC",
            Phone = "+1 (555) 123-4567",
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            UpdatedAt = DateTime.UtcNow.AddMonths(-6),
            Subscriptions = new List<Subscription>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer1Id,
                    Plan = "Pro",
                    Status = SubscriptionStatus.Active,
                    Price = 4900,
                    BillingCycle = "monthly",
                    StartDate = DateTime.UtcNow.AddMonths(-5),
                    EndDate = DateTime.UtcNow.AddMonths(7),
                    CreatedAt = DateTime.UtcNow.AddMonths(-5),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-5),
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer1Id,
                    Plan = "Basic",
                    Status = SubscriptionStatus.Cancelled,
                    Price = 1900,
                    BillingCycle = "monthly",
                    StartDate = DateTime.UtcNow.AddMonths(-12),
                    EndDate = DateTime.UtcNow.AddMonths(-6),
                    CreatedAt = DateTime.UtcNow.AddMonths(-12),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-6),
                    Notes = "Customer switched to the Pro plan"
                }
            }
        };

        var customer2 = new Customer
        {
            Id = customer2Id,
            Name = "Maria Sokolova",
            Email = "maria@startup.io",
            Company = "StartupIO",
            Phone = "+1 (555) 765-4321",
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            UpdatedAt = DateTime.UtcNow.AddMonths(-3),
            Subscriptions = new List<Subscription>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer2Id,
                    Plan = "Enterprise",
                    Status = SubscriptionStatus.Active,
                    Price = 19900,
                    BillingCycle = "annual",
                    StartDate = DateTime.UtcNow.AddMonths(-3),
                    EndDate = DateTime.UtcNow.AddMonths(9),
                    CreatedAt = DateTime.UtcNow.AddMonths(-3),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-3),
                }
            }
        };

        var customer3 = new Customer
        {
            Id = customer3Id,
            Name = "Dmitry Lebedev",
            Email = "dm@freelance.com",
            Company = null,
            Phone = null,
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            UpdatedAt = DateTime.UtcNow.AddMonths(-1),
            Subscriptions = new List<Subscription>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer3Id,
                    Plan = "Starter",
                    Status = SubscriptionStatus.Paused,
                    Price = 990,
                    BillingCycle = "monthly",
                    StartDate = DateTime.UtcNow.AddMonths(-1),
                    EndDate = null,
                    CreatedAt = DateTime.UtcNow.AddMonths(-1),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5),
                    Notes = "Paused at customer's request"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer3Id,
                    Plan = "Pro",
                    Status = SubscriptionStatus.Future,
                    Price = 4900,
                    BillingCycle = "monthly",
                    StartDate = DateTime.UtcNow.AddMonths(1),
                    EndDate = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Notes = "Scheduled for next month"
                }
            }
        };

        _customers[customer1.Id] = customer1;
        _customers[customer2.Id] = customer2;
        _customers[customer3.Id] = customer3;
    }
}
