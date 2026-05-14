using SubscriptionManager.Api.DTOs;
using SubscriptionManager.Api.Models;
using SubscriptionManager.Api.Repositories;

namespace SubscriptionManager.Api.Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _repository;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(ICustomerRepository repository, ILogger<CustomerService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<CustomerDto>> GetAllAsync()
    {
        var customers = await _repository.GetAllAsync();
        return customers.Select(c => c.ToDto());
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id)
    {
        var customer = await _repository.GetByIdAsync(id);
        return customer?.ToDto();
    }

    public async Task<ServiceResult<CustomerDto>> CreateAsync(CreateCustomerDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return ServiceResult<CustomerDto>.BadRequest("Customer name is required.");

        if (string.IsNullOrWhiteSpace(dto.Email))
            return ServiceResult<CustomerDto>.BadRequest("Customer email is required.");

        var existing = await _repository.GetByEmailAsync(dto.Email);
        if (existing is not null)
            return ServiceResult<CustomerDto>.Conflict($"Customer with email '{dto.Email}' already exists.");

        var customer = new Customer
        {
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            Company = dto.Company?.Trim(),
            Phone = dto.Phone?.Trim(),
        };

        var created = await _repository.CreateAsync(customer);
        _logger.LogInformation("Created customer {Id} ({Email})", created.Id, created.Email);

        return ServiceResult<CustomerDto>.Success(created.ToDto());
    }

    public async Task<ServiceResult<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerDto dto)
    {
        var customer = await _repository.GetByIdAsync(id);
        if (customer is null)
            return ServiceResult<CustomerDto>.NotFound($"Customer {id} not found.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            return ServiceResult<CustomerDto>.BadRequest("Customer name is required.");

        if (string.IsNullOrWhiteSpace(dto.Email))
            return ServiceResult<CustomerDto>.BadRequest("Customer email is required.");

        // Check email uniqueness excluding current customer
        var emailOwner = await _repository.GetByEmailAsync(dto.Email);
        if (emailOwner is not null && emailOwner.Id != id)
            return ServiceResult<CustomerDto>.Conflict($"Email '{dto.Email}' is already used by another customer.");

        customer.Name = dto.Name.Trim();
        customer.Email = dto.Email.Trim().ToLowerInvariant();
        customer.Company = dto.Company?.Trim();
        customer.Phone = dto.Phone?.Trim();
        customer.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(customer);
        _logger.LogInformation("Updated customer {Id}", id);

        return ServiceResult<CustomerDto>.Success(updated!.ToDto());
    }

    public async Task<ServiceResult> DeleteAsync(Guid id)
    {
        var exists = await _repository.ExistsAsync(id);
        if (!exists)
            return ServiceResult.NotFound($"Customer {id} not found.");

        await _repository.DeleteAsync(id);
        _logger.LogInformation("Deleted customer {Id}", id);

        return ServiceResult.Success();
    }
}
