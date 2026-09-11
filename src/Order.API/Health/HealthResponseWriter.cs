using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Order.API.Health;

public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report, string serviceName)
    {
        context.Response.ContentType = "application/json";

        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            new
            {
                status = report.Status.ToString(),
                service = serviceName,
                dependencies = report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new
                    {
                        status = entry.Value.Status.ToString(),
                        description = entry.Value.Description
                    })
            },
            cancellationToken: context.RequestAborted);
    }
}
