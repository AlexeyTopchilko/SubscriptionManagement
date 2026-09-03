namespace SubscriptionManager.Api.Models;

/// <summary>
/// Subscription lifecycle state. Transitions are enforced by
/// <c>SubscriptionService.ValidateStatusTransition</c>.
/// </summary>
/// <remarks>
/// ⚠️ THE ORDER OF THESE MEMBERS IS A STORAGE CONTRACT.
///
/// Since persistence was introduced, this enum is stored as its underlying
/// integer — Active=0, Paused=1, Cancelled=2, Future=3. Reordering these members,
/// or inserting a new one anywhere but the end, silently reinterprets every row
/// already in the database: a cancelled subscription would read back as something
/// else. Nothing would fail; the data would just be wrong.
///
/// Add new members at the end only. Never reorder.
///
/// The wire format is insulated from this: JSON serialises the enum by name via
/// JsonStringEnumConverter (see Program.cs), so clients see "Active", not 0. That
/// makes this a storage-layer trap exclusively — which is why it needs a comment
/// rather than being self-evident from the API.
/// </remarks>
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
