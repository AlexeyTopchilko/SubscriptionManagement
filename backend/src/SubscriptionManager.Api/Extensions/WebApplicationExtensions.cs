using Microsoft.EntityFrameworkCore;
using SubscriptionManager.Api.Data;

namespace SubscriptionManager.Api.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Waits for storage to become reachable, applies pending migrations, and
    /// seeds an empty database — all before the application starts serving.
    /// </summary>
    /// <remarks>
    /// FR-005: the wait is bounded. A database that is merely still starting is a
    /// normal condition, not a fault — `docker-compose up` starts everything at
    /// once and Postgres needs a few seconds — so failing on the first refused
    /// connection would break FR-023's "one command brings up a working stack".
    /// Equally, waiting forever would leave the application looking healthy while
    /// being unable to serve anything, which is the state FR-005 exists to prevent.
    /// So: retry up to a configured limit, then fail loudly naming the database.
    ///
    /// Failing here is deliberate. An application that starts without storage would
    /// answer every request with an error a caller cannot interpret.
    /// </remarks>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("SubscriptionManager.Api.Startup");

        var timeout = TimeSpan.FromSeconds(
            app.Configuration.GetValue("Database:ReadinessTimeoutSeconds", 30));
        var retryInterval = TimeSpan.FromSeconds(
            app.Configuration.GetValue("Database:ReadinessRetryIntervalSeconds", 1));

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await WaitForDatabaseAsync(db, logger, timeout, retryInterval);

        logger.LogInformation("Applying database migrations.");
        await db.Database.MigrateAsync();

        await DatabaseSeeder.SeedAsync(db, logger);

        logger.LogInformation("Database ready.");
    }

    private static async Task WaitForDatabaseAsync(
        AppDbContext db,
        ILogger logger,
        TimeSpan timeout,
        TimeSpan retryInterval)
    {
        var deadline = DateTime.UtcNow + timeout;
        var attempt = 0;
        Exception? last = null;

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            try
            {
                if (await db.Database.CanConnectAsync())
                {
                    if (attempt > 1)
                        logger.LogInformation(
                            "Database accepted a connection on attempt {Attempt}.", attempt);
                    return;
                }

                last = null;
            }
            catch (Exception ex)
            {
                // Expected while the server is still starting. Recorded, not
                // rethrown — the caller only needs the reason if the wait expires.
                last = ex;
            }

            logger.LogInformation(
                "Database not ready yet (attempt {Attempt}); retrying in {Interval}s.",
                attempt, retryInterval.TotalSeconds);

            await Task.Delay(retryInterval);
        }

        // Names the database explicitly, so an operator can tell this apart from an
        // application fault without reading storage logs (SC-006).
        var message =
            $"Database did not become reachable within {timeout.TotalSeconds:0}s " +
            $"({attempt} attempts). The application will not start without storage. " +
            "Check that the database is running and that the connection string is correct.";

        logger.LogCritical(last, "{Message}", message);
        throw new InvalidOperationException(message, last);
    }
}
