using Microsoft.EntityFrameworkCore;
using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.Data;

/// <summary>
/// Writes the three demonstration records, and only into an empty database.
/// </summary>
/// <remarks>
/// FR-004: seeding happens when, and only when, the database holds no customers.
/// On every later start nothing is seeded, nothing is restored and nothing is
/// reconciled — so an edited demonstration record keeps its edit and a deleted
/// one stays deleted (User Story 1, scenario 3).
///
/// Deliberately NOT EF Core's <c>HasData</c>. Model seeding reconciles on every
/// migration, which would resurrect a demonstration customer someone deleted —
/// exactly the behaviour FR-004 forbids.
///
/// The values reproduce what <c>InMemoryCustomerRepository.SeedData()</c> used to
/// create, including the relative timestamps. The API baseline reads these records,
/// so changing them changes the recorded contract.
/// </remarks>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (await db.Customers.AnyAsync(ct))
        {
            logger.LogInformation("Database already contains customers; seeding skipped.");
            return;
        }

        logger.LogInformation("Empty database detected; writing demonstration records.");

        // Captured once so every relative offset below is measured from the same
        // instant. Using UtcNow per-property would make the seeded data internally
        // inconsistent by a few milliseconds.
        var now = DateTime.UtcNow;

        var customer1Id = Guid.NewGuid();
        var customer2Id = Guid.NewGuid();
        var customer3Id = Guid.NewGuid();

        var customer1 = new Customer
        {
            Id = customer1Id,
            Name = "Alex Gromov",
            Email = "alex@company.com",
            Company = "Technologies LLC",
            Phone = "+1 (555) 123-4567",
            CreatedAt = now.AddMonths(-6),
            UpdatedAt = now.AddMonths(-6),
            Subscriptions = new List<Subscription>
            {
                // NOTE: this pair is the reason for the one declared contract change.
                // "Pro" is listed first but was created LATER than "Basic", so the
                // insertion order does not match creation order. Under FR-020's
                // ORDER BY CreatedAt they swap, which is expected and pre-declared.
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer1Id,
                    Plan = "Pro",
                    Status = SubscriptionStatus.Active,
                    Price = 4900,
                    BillingCycle = "monthly",
                    StartDate = now.AddMonths(-5),
                    EndDate = now.AddMonths(7),
                    CreatedAt = now.AddMonths(-5),
                    UpdatedAt = now.AddMonths(-5),
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer1Id,
                    Plan = "Basic",
                    Status = SubscriptionStatus.Cancelled,
                    Price = 1900,
                    BillingCycle = "monthly",
                    StartDate = now.AddMonths(-12),
                    EndDate = now.AddMonths(-6),
                    CreatedAt = now.AddMonths(-12),
                    UpdatedAt = now.AddMonths(-6),
                    Notes = "Customer switched to the Pro plan"
                }
            }
        };

        var customer2 = new Customer
        {
            Id = customer2Id,
            Name = "Maria Sokolova",
            Email = "maria@startup.io",
            Company = "StartupIO",
            Phone = "+1 (555) 765-4321",
            CreatedAt = now.AddMonths(-3),
            UpdatedAt = now.AddMonths(-3),
            Subscriptions = new List<Subscription>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer2Id,
                    Plan = "Enterprise",
                    Status = SubscriptionStatus.Active,
                    Price = 19900,
                    BillingCycle = "annual",
                    StartDate = now.AddMonths(-3),
                    EndDate = now.AddMonths(9),
                    CreatedAt = now.AddMonths(-3),
                    UpdatedAt = now.AddMonths(-3),
                }
            }
        };

        var customer3 = new Customer
        {
            Id = customer3Id,
            Name = "Dmitry Lebedev",
            Email = "dm@freelance.com",
            Company = null,
            Phone = null,
            CreatedAt = now.AddMonths(-1),
            UpdatedAt = now.AddMonths(-1),
            Subscriptions = new List<Subscription>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer3Id,
                    Plan = "Starter",
                    Status = SubscriptionStatus.Paused,
                    Price = 990,
                    BillingCycle = "monthly",
                    StartDate = now.AddMonths(-1),
                    EndDate = null,
                    CreatedAt = now.AddMonths(-1),
                    UpdatedAt = now.AddDays(-5),
                    Notes = "Paused at customer's request"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer3Id,
                    Plan = "Pro",
                    Status = SubscriptionStatus.Future,
                    Price = 4900,
                    BillingCycle = "monthly",
                    StartDate = now.AddMonths(1),
                    EndDate = null,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Notes = "Scheduled for next month"
                }
            }
        };

        db.Customers.AddRange(customer1, customer2, customer3);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded 3 customers with 5 subscriptions.");
    }
}
