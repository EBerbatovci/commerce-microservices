using Microsoft.Extensions.Diagnostics.HealthChecks;
using Order.API.Clients;

namespace Order.API.Health;

public sealed class CatalogApiHealthCheck(CatalogClient catalogClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await catalogClient.IsHealthyAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Catalog API is ready.")
                : HealthCheckResult.Unhealthy("Catalog API is not ready.");
        }
        catch (CatalogUnavailableException exception)
        {
            return HealthCheckResult.Unhealthy("Catalog API is unavailable.", exception);
        }
    }
}
