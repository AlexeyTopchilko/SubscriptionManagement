using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.Repositories;

public interface ISubscriptionRepository
{
    Task<IEnumerable<Subscription>> GetByCustomerIdAsync(Guid customerId);
    Task<Subscription?> GetByIdAsync(Guid customerId, Guid id);
    Task<Subscription> CreateAsync(Subscription subscription);
    Task<Subscription?> UpdateAsync(Subscription subscription);
    Task<bool> DeleteAsync(Guid customerId, Guid id);
}
