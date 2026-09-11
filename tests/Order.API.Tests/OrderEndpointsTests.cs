using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Order.API.Tests;

public sealed class OrderEndpointsTests(OrderApiFactory factory)
    : IClassFixture<OrderApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateOrder_PropagatesCorrelationIdToCatalog()
    {
        var productId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();
        factory.Catalog.Reset();
        factory.Catalog.AddProduct(productId, "Traced Product", 42m, 3);

        using var request = CreateOrderRequest(productId, 1);
        request.Headers.Add("X-Correlation-ID", correlationId);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal(correlationId, factory.Catalog.LastCorrelationId);
    }

    [Theory]
    [InlineData("/health/live", "application")]
    [InlineData("/health/ready", "catalog")]
    public async Task HealthEndpoint_ReturnsStructuredHealthyResponse(
        string path,
        string expectedDependency)
    {
        factory.Catalog.Reset();

        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var health = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", health.RootElement.GetProperty("status").GetString());
        Assert.Equal("Order.API", health.RootElement.GetProperty("service").GetString());
        Assert.Equal(
            "Healthy",
            health.RootElement.GetProperty("dependencies")
                .GetProperty(expectedDependency)
                .GetProperty("status")
                .GetString());
    }

    [Fact]
    public async Task CreateOrder_ReturnsCreatedForValidOrder()
    {
        var productId = Guid.NewGuid();
        factory.Catalog.Reset();
        factory.Catalog.AddProduct(productId, "Catalog Keyboard", 89.50m, 10);

        var response = await PostOrderAsync(productId, 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task CreateOrder_UsesProductNameAndPriceFromCatalog()
    {
        var productId = Guid.NewGuid();
        factory.Catalog.Reset();
        factory.Catalog.AddProduct(productId, "Catalog Monitor", 329.90m, 5);

        var response = await PostOrderAsync(productId, 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var order = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = order.RootElement.GetProperty("items")[0];
        Assert.Equal("Catalog Monitor", item.GetProperty("productName").GetString());
        Assert.Equal(329.90m, item.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(659.80m, order.RootElement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task CreateOrder_ReturnsValidationProblemForUnknownProduct()
    {
        factory.Catalog.Reset();

        var response = await PostOrderAsync(Guid.NewGuid(), 1);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(1, factory.Catalog.RequestCount);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains(problem?.Errors["Items"] ?? [], message => message.Contains("does not exist"));
    }

    [Fact]
    public async Task CreateOrder_ReturnsValidationProblemForInsufficientStock()
    {
        var productId = Guid.NewGuid();
        factory.Catalog.Reset();
        factory.Catalog.AddProduct(productId, "Low-stock Product", 25m, 1);

        var response = await PostOrderAsync(productId, 2);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains(problem?.Errors["Items"] ?? [], message => message.Contains("insufficient stock"));
    }

    [Fact]
    public async Task CreateOrder_ReturnsProblemDetailsWhenCatalogIsUnavailable()
    {
        factory.Catalog.Reset();
        factory.Catalog.IsUnavailable = true;

        var response = await PostOrderAsync(Guid.NewGuid(), 1);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem?.Status);
        Assert.Equal("Catalog API is unavailable.", problem?.Title);
    }

    private Task<HttpResponseMessage> PostOrderAsync(Guid productId, int quantity) =>
        _client.SendAsync(CreateOrderRequest(productId, quantity));

    private static HttpRequestMessage CreateOrderRequest(Guid productId, int quantity) =>
        new(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                customerEmail = "integration-tests@example.com",
                items = new[]
                {
                    new { productId, quantity }
                }
            })
        };

    private static Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode statusCode)
    {
        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return Task.CompletedTask;
    }
}
