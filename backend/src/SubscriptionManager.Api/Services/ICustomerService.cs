using SubscriptionManager.Api.DTOs;

namespace SubscriptionManager.Api.Services;

public interface ICustomerService
{
    Task<IEnumerable<CustomerDto>> GetAllAsync();
    Task<CustomerDto?> GetByIdAsync(Guid id);
    Task<ServiceResult<CustomerDto>> CreateAsync(CreateCustomerDto dto);
    Task<ServiceResult<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerDto dto);
    Task<ServiceResult> DeleteAsync(Guid id);
}
