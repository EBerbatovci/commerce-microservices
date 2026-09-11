using Gateway.API.Health;
using Gateway.API.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("catalog-health", client =>
{
    client.BaseAddress = new Uri(builder.Configuration[
        "ReverseProxy:Clusters:catalog-cluster:Destinations:catalog-api:Address"]
        ?? throw new InvalidOperationException("Catalog destination is not configured."));
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddHttpClient("order-health", client =>
{
    client.BaseAddress = new Uri(builder.Configuration[
        "ReverseProxy:Clusters:orders-cluster:Destinations:order-api:Address"]
        ?? throw new InvalidOperationException("Order destination is not configured."));
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddHealthChecks()
    .AddCheck(
        "application",
        () => HealthCheckResult.Healthy("Commerce API Gateway is running."),
        tags: ["live", "ready"])
    .AddCheck<CatalogApiHealthCheck>("catalog", tags: ["ready"])
    .AddCheck<OrderApiHealthCheck>("order", tags: ["ready"]);
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapGet("/", () => Results.Ok(new
{
    service = "Commerce API Gateway",
    routes = new[]
    {
        "/catalog/api/products",
        "/orders/api/orders"
    }
}));

var readinessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = (context, report) =>
        HealthResponseWriter.WriteAsync(context, report, "Gateway.API")
};
var livenessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = (context, report) =>
        HealthResponseWriter.WriteAsync(context, report, "Gateway.API")
};

app.MapHealthChecks("/health", readinessOptions);
app.MapHealthChecks("/health/live", livenessOptions);
app.MapHealthChecks("/health/ready", readinessOptions);
app.MapReverseProxy();

await app.RunAsync();

public partial class Program;
