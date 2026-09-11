using Catalog.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.API.Health;

public sealed class CatalogDatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Catalog SQLite database is available.")
                : HealthCheckResult.Unhealthy("Catalog SQLite database is unavailable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Catalog SQLite database is unavailable.",
                exception);
        }
    }
}
