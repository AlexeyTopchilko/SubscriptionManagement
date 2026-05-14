using SubscriptionManager.Api.DTOs;
using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.Services;

public interface ISubscriptionService
{
    Task<IEnumerable<SubscriptionDto>> GetByCustomerIdAsync(Guid customerId);
    Task<SubscriptionDto?> GetByIdAsync(Guid customerId, Guid id);
    Task<ServiceResult<SubscriptionDto>> CreateAsync(Guid customerId, CreateSubscriptionDto dto);
    Task<ServiceResult<SubscriptionDto>> UpdateAsync(Guid customerId, Guid id, UpdateSubscriptionDto dto);
    Task<ServiceResult<SubscriptionDto>> UpdateStatusAsync(Guid customerId, Guid id, SubscriptionStatus status);
    Task<ServiceResult> DeleteAsync(Guid customerId, Guid id);
}
