using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Order.API.Data;

namespace Order.API.Health;

public sealed class OrderDatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Order SQLite database is available.")
                : HealthCheckResult.Unhealthy("Order SQLite database is unavailable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Order SQLite database is unavailable.",
                exception);
        }
    }
}
