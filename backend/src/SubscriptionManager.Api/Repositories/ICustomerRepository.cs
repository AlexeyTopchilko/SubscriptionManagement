using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.Repositories;

public interface ICustomerRepository
{
    Task<IEnumerable<Customer>> GetAllAsync();
    Task<Customer?> GetByIdAsync(Guid id);
    Task<Customer?> GetByEmailAsync(string email);
    Task<Customer> CreateAsync(Customer customer);
    Task<Customer?> UpdateAsync(Customer customer);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
}
