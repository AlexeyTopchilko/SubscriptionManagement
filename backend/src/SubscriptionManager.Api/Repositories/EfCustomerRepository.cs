using Microsoft.EntityFrameworkCore;
using SubscriptionManager.Api.Data;
using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.Repositories;

/// <summary>
/// Database-backed <see cref="ICustomerRepository"/>. Replaces the in-memory
/// implementation; the interface is unchanged.
/// </summary>
/// <remarks>
/// Tracking is deliberately asymmetric, and it matters:
/// <see cref="GetByIdAsync"/> returns a <em>tracked</em> entity because
/// <c>CustomerService.UpdateAsync</c> loads a customer, mutates it in place and
/// then calls <see cref="UpdateAsync"/>. Reads that are never mutated use
/// <c>AsNoTracking</c>.
/// </remarks>
public class EfCustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;

    public EfCustomerRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        // FR-019 to FR-021: customers oldest-created first, Id as the stable
        // tiebreaker, and each customer's subscriptions ordered the same way.
        // The ordered Include pushes both sorts into SQL.
        //
        // The customer order matches the previous in-memory behaviour, which also
        // sorted by CreatedAt. The subscription order is the one declared change:
        // the list used to come back in insertion order.
        return await _db.Customers
            .AsNoTracking()
            .Include(c => c.Subscriptions.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id))
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync();
    }

    public async Task<Customer?> GetByIdAsync(Guid id)
    {
        // Tracked on purpose — see the remarks above. Switching this to
        // AsNoTracking silently breaks every update: the service's mutations would
        // be made to a detached object and SaveChanges would persist nothing.
        return await _db.Customers
            .Include(c => c.Subscriptions.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id))
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Customer?> GetByEmailAsync(string email)
    {
        // Case-insensitive, and that is not cosmetic. The in-memory implementation
        // compared with OrdinalIgnoreCase, and CustomerService calls this with the
        // caller's RAW email — before it lower-cases the value it stores. A
        // case-sensitive `=` here would fail to find "ALEX@COMPANY.COM" against the
        // stored "alex@company.com", let a duplicate through, and turn baseline
        // case 9 from 409 into 201.
        //
        // ToLower() translates to SQL lower(); no index exists on Email (see
        // AppDbContext for why), so there is nothing to defeat.
        var normalized = (email ?? string.Empty).ToLower();

        return await _db.Customers
            .AsNoTracking()
            .Include(c => c.Subscriptions.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id))
            .FirstOrDefaultAsync(c => c.Email.ToLower() == normalized);
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    public async Task<Customer?> UpdateAsync(Customer customer)
    {
        // The service has already loaded this customer through GetByIdAsync, so it
        // is tracked and its mutations are pending. Saving is all that is needed.
        // Attach() would throw on an already-tracked instance.
        if (!await _db.Customers.AnyAsync(c => c.Id == customer.Id))
            return null;

        if (_db.Entry(customer).State == EntityState.Detached)
            _db.Customers.Update(customer);

        await _db.SaveChangesAsync();
        return customer;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null)
            return false;

        // Subscriptions go with it, by the cascade configured in AppDbContext —
        // FR-003, enforced by the schema rather than by remembering to do it here.
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id) =>
        await _db.Customers.AnyAsync(c => c.Id == id);
}
