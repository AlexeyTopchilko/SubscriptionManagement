using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.DTOs;

public record CreateSubscriptionDto(
    string Plan,
    decimal Price,
    string BillingCycle,
    DateTime StartDate,
    DateTime? EndDate,
    string? Notes
);

public record UpdateSubscriptionDto(
    string Plan,
    decimal Price,
    string BillingCycle,
    DateTime StartDate,
    DateTime? EndDate,
    string? Notes
);

public record UpdateSubscriptionStatusDto(
    SubscriptionStatus Status
);

public record SubscriptionDto(
    Guid Id,
    Guid CustomerId,
    string Plan,
    SubscriptionStatus Status,
    decimal Price,
    string BillingCycle,
    DateTime StartDate,
    DateTime? EndDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? Notes
);

public static class SubscriptionMappings
{
    public static SubscriptionDto ToDto(this Subscription s) => new(
        s.Id,
        s.CustomerId,
        s.Plan,
        s.Status,
        s.Price,
        s.BillingCycle,
        s.StartDate,
        s.EndDate,
        s.CreatedAt,
        s.UpdatedAt,
        s.Notes
    );
}
