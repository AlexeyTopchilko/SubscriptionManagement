using SubscriptionManager.Api.DTOs;
using SubscriptionManager.Api.Models;
using SubscriptionManager.Api.Repositories;

namespace SubscriptionManager.Api.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILogger<SubscriptionService> _logger;

    private static readonly string[] AllowedBillingCycles = { "monthly", "annual" };
    private static readonly string[] AllowedPlans = { "Starter", "Basic", "Pro", "Enterprise" };

    public SubscriptionService(
        ISubscriptionRepository subscriptionRepository,
        ICustomerRepository customerRepository,
        ILogger<SubscriptionService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _customerRepository = customerRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<SubscriptionDto>> GetByCustomerIdAsync(Guid customerId)
    {
        var subscriptions = await _subscriptionRepository.GetByCustomerIdAsync(customerId);
        return subscriptions.Select(s => s.ToDto());
    }

    public async Task<SubscriptionDto?> GetByIdAsync(Guid customerId, Guid id)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(customerId, id);
        return subscription?.ToDto();
    }

    public async Task<ServiceResult<SubscriptionDto>> CreateAsync(Guid customerId, CreateSubscriptionDto dto)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
            return ServiceResult<SubscriptionDto>.NotFound($"Customer {customerId} not found.");

        var validationError = ValidateSubscriptionDto(dto.Plan, dto.Price, dto.BillingCycle, dto.StartDate, dto.EndDate);
        if (validationError is not null)
            return ServiceResult<SubscriptionDto>.BadRequest(validationError);

        var subscription = new Subscription
        {
            CustomerId = customerId,
            Plan = dto.Plan.Trim(),
            Status = dto.StartDate > DateTime.UtcNow
                ? SubscriptionStatus.Future
                : SubscriptionStatus.Active,
            Price = dto.Price,
            BillingCycle = dto.BillingCycle.ToLowerInvariant(),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Notes = dto.Notes?.Trim(),
        };

        var created = await _subscriptionRepository.CreateAsync(subscription);
        _logger.LogInformation("Created subscription {Id} for customer {CustomerId}", created.Id, customerId);

        return ServiceResult<SubscriptionDto>.Success(created.ToDto());
    }

    public async Task<ServiceResult<SubscriptionDto>> UpdateAsync(Guid customerId, Guid id, UpdateSubscriptionDto dto)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(customerId, id);
        if (subscription is null)
            return ServiceResult<SubscriptionDto>.NotFound($"Subscription {id} not found.");

        var validationError = ValidateSubscriptionDto(dto.Plan, dto.Price, dto.BillingCycle, dto.StartDate, dto.EndDate);
        if (validationError is not null)
            return ServiceResult<SubscriptionDto>.BadRequest(validationError);

        subscription.Plan = dto.Plan.Trim();
        subscription.Price = dto.Price;
        subscription.BillingCycle = dto.BillingCycle.ToLowerInvariant();
        subscription.StartDate = dto.StartDate;
        subscription.EndDate = dto.EndDate;
        subscription.Notes = dto.Notes?.Trim();
        subscription.UpdatedAt = DateTime.UtcNow;

        var updated = await _subscriptionRepository.UpdateAsync(subscription);
        _logger.LogInformation("Updated subscription {Id}", id);

        return ServiceResult<SubscriptionDto>.Success(updated!.ToDto());
    }

    public async Task<ServiceResult<SubscriptionDto>> UpdateStatusAsync(Guid customerId, Guid id, SubscriptionStatus status)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(customerId, id);
        if (subscription is null)
            return ServiceResult<SubscriptionDto>.NotFound($"Subscription {id} not found.");

        var transitionError = ValidateStatusTransition(subscription.Status, status);
        if (transitionError is not null)
            return ServiceResult<SubscriptionDto>.BadRequest(transitionError);

        subscription.Status = status;
        subscription.UpdatedAt = DateTime.UtcNow;

        var updated = await _subscriptionRepository.UpdateAsync(subscription);
        _logger.LogInformation("Changed subscription status {Id}: {Status}", id, status);

        return ServiceResult<SubscriptionDto>.Success(updated!.ToDto());
    }

    public async Task<ServiceResult> DeleteAsync(Guid customerId, Guid id)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(customerId, id);
        if (subscription is null)
            return ServiceResult.NotFound($"Subscription {id} not found.");

        await _subscriptionRepository.DeleteAsync(customerId, id);
        _logger.LogInformation("Deleted subscription {Id}", id);

        return ServiceResult.Success();
    }

    private static string? ValidateSubscriptionDto(
        string plan, decimal price, string billingCycle,
        DateTime startDate, DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(plan))
            return "Plan name is required.";

        if (price < 0)
            return "Price cannot be negative.";

        if (!AllowedBillingCycles.Contains(billingCycle.ToLowerInvariant()))
            return $"Allowed billing cycles: {string.Join(", ", AllowedBillingCycles)}.";

        if (endDate.HasValue && endDate.Value <= startDate)
            return "End date must be later than start date.";

        return null;
    }

    private static string? ValidateStatusTransition(SubscriptionStatus current, SubscriptionStatus next)
    {
        // Define allowed transitions
        var allowed = new Dictionary<SubscriptionStatus, HashSet<SubscriptionStatus>>
        {
            [SubscriptionStatus.Future] = new() { SubscriptionStatus.Active, SubscriptionStatus.Cancelled },
            [SubscriptionStatus.Active] = new() { SubscriptionStatus.Paused, SubscriptionStatus.Cancelled },
            [SubscriptionStatus.Paused] = new() { SubscriptionStatus.Active, SubscriptionStatus.Cancelled },
            [SubscriptionStatus.Cancelled] = new(), // terminal state
        };

        if (current == next) return null; // no-op is fine

        if (!allowed[current].Contains(next))
            return $"Status transition '{current}' → '{next}' is not allowed.";

        return null;
    }
}
