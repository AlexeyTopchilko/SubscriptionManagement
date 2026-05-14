using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.DTOs;

public record CreateCustomerDto(
    string Name,
    string Email,
    string? Company,
    string? Phone
);

public record UpdateCustomerDto(
    string Name,
    string Email,
    string? Company,
    string? Phone
);

public record CustomerDto(
    Guid Id,
    string Name,
    string Email,
    string? Company,
    string? Phone,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<SubscriptionDto> Subscriptions
);

public static class CustomerMappings
{
    public static CustomerDto ToDto(this Customer c) => new(
        c.Id,
        c.Name,
        c.Email,
        c.Company,
        c.Phone,
        c.CreatedAt,
        c.UpdatedAt,
        c.Subscriptions.Select(s => s.ToDto()).ToList()
    );
}
