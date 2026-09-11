using System.Net;
using System.Net.Http.Json;

namespace Order.API.Tests;

public sealed class StubCatalogHandler : HttpMessageHandler
{
    private readonly Dictionary<Guid, StubProduct> _products = [];

    public bool IsUnavailable { get; set; }
    public string? LastCorrelationId { get; private set; }
    public int RequestCount { get; private set; }

    public void Reset()
    {
        _products.Clear();
        IsUnavailable = false;
        LastCorrelationId = null;
        RequestCount = 0;
    }

    public void AddProduct(Guid id, string name, decimal price, int stock) =>
        _products[id] = new StubProduct(id, name, price, stock);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastCorrelationId = request.Headers.TryGetValues("X-Correlation-ID", out var values)
            ? values.SingleOrDefault()
            : null;

        if (IsUnavailable)
        {
            throw new HttpRequestException("Catalog API is unavailable.");
        }

        if (request.RequestUri?.AbsolutePath == "/health/ready")
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { status = "Healthy" })
            });
        }

        var idSegment = request.RequestUri?.Segments.LastOrDefault()?.TrimEnd('/');
        if (!Guid.TryParse(idSegment, out var productId) || !_products.TryGetValue(productId, out var product))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(product)
        });
    }

    private sealed record StubProduct(Guid Id, string Name, decimal Price, int Stock);
}
