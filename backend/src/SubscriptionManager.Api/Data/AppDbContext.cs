using Microsoft.EntityFrameworkCore;
using SubscriptionManager.Api.Models;

namespace SubscriptionManager.Api.Data;

/// <summary>
/// The only place Entity Framework appears in this project. Confined to the
/// repository layer so Principle III (Controllers → Services → Repositories)
/// holds: no controller and no service references EF Core.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// Customers only. There is deliberately NO <c>DbSet&lt;Subscription&gt;</c>.
    /// Principle I binds the access path: a subscription must be unreachable
    /// without first resolving its customer, and exposing a set here would open
    /// exactly the second access path the principle forbids. Subscription is
    /// still a mapped entity — EF discovers it through the navigation below.
    /// </summary>
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(customer =>
        {
            customer.ToTable("Customers");
            customer.HasKey(c => c.Id);

            customer.Property(c => c.Id).HasColumnType("uuid").ValueGeneratedNever();
            customer.Property(c => c.Name).IsRequired();
            customer.Property(c => c.Email).IsRequired();
            customer.Property(c => c.CreatedAt).HasColumnType("timestamp with time zone");
            customer.Property(c => c.UpdatedAt).HasColumnType("timestamp with time zone");

            // Serves FR-020 and FR-021: customers oldest-created first, with Id as
            // the stable tiebreaker. Indexed so the sort is satisfied by the index.
            customer.HasIndex(c => new { c.CreatedAt, c.Id });

            // NOTE: no unique index on Email, deliberately. The duplicate-email
            // check lives in CustomerService and returns a 409 (baseline case 9).
            // A unique index would turn that into a DbUpdateException reaching the
            // global middleware as a 500 — a client's ordinary mistake reported as
            // a server fault, breaking FR-015 and Principle V.

            customer.HasMany(c => c.Subscriptions)
                .WithOne()
                .HasForeignKey(s => s.CustomerId)
                // FR-003: a subscription must not outlive its customer. Enforced by
                // the schema rather than by application code remembering to clean up.
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Subscription>(subscription =>
        {
            subscription.ToTable("Subscriptions");
            subscription.HasKey(s => s.Id);

            subscription.Property(s => s.Id).HasColumnType("uuid").ValueGeneratedNever();
            subscription.Property(s => s.CustomerId).HasColumnType("uuid");
            subscription.Property(s => s.Plan).IsRequired();
            subscription.Property(s => s.BillingCycle).IsRequired();

            // The single most consequential line in this feature.
            //
            // `numeric` with NO precision and NO scale. A declared scale pads every
            // amount to that many decimal places, so numeric(18,2) returns 4900 as
            // 4900.00 — and System.Text.Json puts the scale on the wire, breaking
            // the contract the baseline pins. Measured, not assumed: see
            // specs/001-durable-storage/research.md.
            //
            // Do not "tighten" this by adding precision or scale. It looks like
            // hardening and it is a contract break (FR-009, FR-024).
            subscription.Property(s => s.Price).HasColumnType("numeric");

            // Persisted as its underlying integer. This makes the DECLARATION ORDER
            // of SubscriptionStatus a storage contract: reordering its members, or
            // inserting one anywhere but the end, silently reinterprets every stored
            // row. The wire format is insulated because JSON serialises the enum by
            // name (JsonStringEnumConverter in Program.cs), so this is a
            // storage-layer trap only. See the comment on the enum itself.
            subscription.Property(s => s.Status).HasConversion<int>();

            // `timestamp with time zone` requires DateTime.Kind == Utc under Npgsql 6+.
            // Verified safe for every real caller: the SPA submits
            // `new Date(x).toISOString()` and the baseline script sends explicit `Z`,
            // so everything arriving here is already UTC. Residual risk, recorded
            // rather than hidden: a third-party client posting a date with no `Z`
            // would get a 500 where it previously got a 201. No such caller exists,
            // and the shape is absent from the baseline.
            subscription.Property(s => s.StartDate).HasColumnType("timestamp with time zone");
            subscription.Property(s => s.EndDate).HasColumnType("timestamp with time zone");
            subscription.Property(s => s.CreatedAt).HasColumnType("timestamp with time zone");
            subscription.Property(s => s.UpdatedAt).HasColumnType("timestamp with time zone");

            // Serves the foreign-key lookup and the per-customer ordering at once.
            subscription.HasIndex(s => new { s.CustomerId, s.CreatedAt, s.Id });

            // NOTE: no check constraints on Price, StartDate or EndDate, deliberately.
            // Those rules live in SubscriptionService and surface as 400s. A storage
            // constraint would report them as 500s and create a second place the
            // rules can disagree — Principle III, and FR-015.
        });
    }
}
