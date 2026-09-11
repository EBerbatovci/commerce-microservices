using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gateway.API.Health;

public abstract class DownstreamHealthCheck(
    IHttpClientFactory httpClientFactory,
    string clientName) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(clientName);
            using var response = await client.GetAsync("health/ready", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Downstream service is ready.")
                : HealthCheckResult.Unhealthy(
                    $"Downstream service returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Downstream service is unavailable.", exception);
        }
    }
}

public sealed class CatalogApiHealthCheck(IHttpClientFactory httpClientFactory)
    : DownstreamHealthCheck(httpClientFactory, "catalog-health");

public sealed class OrderApiHealthCheck(IHttpClientFactory httpClientFactory)
    : DownstreamHealthCheck(httpClientFactory, "order-health");
