using Microsoft.EntityFrameworkCore;
using SubscriptionManager.Api.Data;
using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.Repositories;

/// <summary>
/// Database-backed <see cref="ISubscriptionRepository"/>. Every operation goes
/// through the parent customer's aggregate; the interface is unchanged.
/// </summary>
/// <remarks>
/// Principle I is the constraint that shapes this class. There is no
/// <c>DbSet&lt;Subscription&gt;</c> to query, and every method here identifies the
/// owning customer first — either by an explicit <c>customerId</c> parameter or,
/// for create and update, by <c>subscription.CustomerId</c> carried on the entity.
/// No path reaches a subscription by its own id alone.
///
/// The customer's <c>UpdatedAt</c> is bumped on every mutation, matching the
/// previous in-memory behaviour: changing a subscription counts as changing its
/// customer, and both are saved together as one unit of change (FR-002).
/// </remarks>
public class EfSubscriptionRepository : ISubscriptionRepository
{
    private readonly AppDbContext _db;

    public EfSubscriptionRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Subscription>> GetByCustomerIdAsync(Guid customerId)
    {
        // FR-020: oldest-created first, Id as the tiebreaker.
        //
        // Note this endpoint previously sorted DESCENDING, while GET /api/customers
        // returned the same collection in insertion order — the API answered with
        // two different orders depending on which route you asked. That
        // inconsistency was invisible in the recorded baseline because it only
        // captures this route for a customer whose two orderings happen to
        // coincide. FR-019 makes one order authoritative everywhere.
        var customer = await _db.Customers
            .AsNoTracking()
            .Include(c => c.Subscriptions.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id))
            .FirstOrDefaultAsync(c => c.Id == customerId);

        return customer?.Subscriptions ?? (IEnumerable<Subscription>)Array.Empty<Subscription>();
    }

    public async Task<Subscription?> GetByIdAsync(Guid customerId, Guid id)
    {
        // Tracked, because SubscriptionService loads a subscription here, mutates
        // it, and calls UpdateAsync. AsNoTracking would make every update and every
        // status change a silent no-op.
        //
        // The customer is resolved first and the subscription is selected from its
        // collection — not fetched by its own id. That is Principle I in practice.
        var customer = await _db.Customers
            .Include(c => c.Subscriptions)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        return customer?.Subscriptions.FirstOrDefault(s => s.Id == id);
    }

    public async Task<Subscription> CreateAsync(Subscription subscription)
    {
        var customer = await _db.Customers
            .Include(c => c.Subscriptions)
            .FirstOrDefaultAsync(c => c.Id == subscription.CustomerId);

        // Kept as a throw for behavioural parity with the in-memory implementation:
        // SubscriptionService checks the customer exists and returns NotFound before
        // reaching here, so this is genuinely unexpected and belongs to the global
        // middleware (Principle II).
        if (customer is null)
            throw new InvalidOperationException($"Customer {subscription.CustomerId} not found.");

        customer.Subscriptions.Add(subscription);
        customer.UpdatedAt = DateTime.UtcNow;

        // One SaveChanges for the customer and its new subscription together.
        await _db.SaveChangesAsync();
        return subscription;
    }

    public async Task<Subscription?> UpdateAsync(Subscription subscription)
    {
        var customer = await _db.Customers
            .Include(c => c.Subscriptions)
            .FirstOrDefaultAsync(c => c.Id == subscription.CustomerId);

        if (customer is null)
            return null;

        var existing = customer.Subscriptions.FirstOrDefault(s => s.Id == subscription.Id);
        if (existing is null)
            return null;

        // The service mutated the instance it got from GetByIdAsync, which is the
        // tracked one, so its changes are already pending. When a detached instance
        // arrives instead, copy the values onto the tracked entity rather than
        // replacing it — swapping the object would orphan the tracked original and
        // could duplicate the row.
        if (!ReferenceEquals(existing, subscription))
            _db.Entry(existing).CurrentValues.SetValues(subscription);

        customer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid customerId, Guid id)
    {
        var customer = await _db.Customers
            .Include(c => c.Subscriptions)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer is null)
            return false;

        var subscription = customer.Subscriptions.FirstOrDefault(s => s.Id == id);
        if (subscription is null)
            return false;

        customer.Subscriptions.Remove(subscription);
        customer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }
}
