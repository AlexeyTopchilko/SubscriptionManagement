using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.Repositories;

public class InMemorySubscriptionRepository : ISubscriptionRepository
{
    private readonly ICustomerRepository _customerRepository;

    public InMemorySubscriptionRepository(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<IEnumerable<Subscription>> GetByCustomerIdAsync(Guid customerId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        return customer?.Subscriptions.OrderByDescending(s => s.CreatedAt)
               ?? Enumerable.Empty<Subscription>();
    }

    public async Task<Subscription?> GetByIdAsync(Guid customerId, Guid id)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        return customer?.Subscriptions.FirstOrDefault(s => s.Id == id);
    }

    public async Task<Subscription> CreateAsync(Subscription subscription)
    {
        var customer = await _customerRepository.GetByIdAsync(subscription.CustomerId);
        if (customer is null)
            throw new InvalidOperationException($"Customer {subscription.CustomerId} not found.");

        customer.Subscriptions.Add(subscription);
        customer.UpdatedAt = DateTime.UtcNow;
        await _customerRepository.UpdateAsync(customer);
        return subscription;
    }

    public async Task<Subscription?> UpdateAsync(Subscription subscription)
    {
        var customer = await _customerRepository.GetByIdAsync(subscription.CustomerId);
        if (customer is null) return null;

        var index = customer.Subscriptions.FindIndex(s => s.Id == subscription.Id);
        if (index < 0) return null;

        customer.Subscriptions[index] = subscription;
        customer.UpdatedAt = DateTime.UtcNow;
        await _customerRepository.UpdateAsync(customer);
        return subscription;
    }

    public async Task<bool> DeleteAsync(Guid customerId, Guid id)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null) return false;

        var subscription = customer.Subscriptions.FirstOrDefault(s => s.Id == id);
        if (subscription is null) return false;

        customer.Subscriptions.Remove(subscription);
        customer.UpdatedAt = DateTime.UtcNow;
        await _customerRepository.UpdateAsync(customer);
        return true;
    }
}
