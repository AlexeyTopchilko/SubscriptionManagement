namespace SubscriptionManager.Api.Models;

public enum SubscriptionStatus
{
    Active,
    Paused,
    Cancelled,
    Future
}

public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string Plan { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Future;
    public decimal Price { get; set; }
    public string BillingCycle { get; set; } = "monthly"; // monthly | annual
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
