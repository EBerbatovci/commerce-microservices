using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Catalog.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.API.Tests;

public sealed class CatalogEndpointsTests(CatalogApiFactory factory)
    : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Response_IncludesGeneratedCorrelationId()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.True(Guid.TryParse(Assert.Single(values), out _));
    }

    [Theory]
    [InlineData("/health/live", "application")]
    [InlineData("/health/ready", "sqlite")]
    public async Task HealthEndpoint_ReturnsStructuredHealthyResponse(
        string path,
        string expectedDependency)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var health = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", health.RootElement.GetProperty("status").GetString());
        Assert.Equal("Catalog.API", health.RootElement.GetProperty("service").GetString());
        Assert.Equal(
            "Healthy",
            health.RootElement.GetProperty("dependencies")
                .GetProperty(expectedDependency)
                .GetProperty("status")
                .GetString());
    }

    [Fact]
    public async Task GetProducts_ReturnsSeededProducts()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<Product>>();
        Assert.NotNull(products);
        Assert.Contains(products, product => product.Name == "Wireless Headphones");
        Assert.Contains(products, product => product.Name == "Mechanical Keyboard");
    }

    [Fact]
    public async Task GetProduct_ReturnsProblemDetailsForUnknownProduct()
    {
        var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(StatusCodes.Status404NotFound, problem?.Status);
    }

    [Fact]
    public async Task CreateProduct_ReturnsValidationProblemForInvalidInput()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            name = "",
            description = "Invalid product",
            price = 0,
            stock = -1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains("name", problem?.Errors.Keys ?? []);
        Assert.Contains("price", problem?.Errors.Keys ?? []);
        Assert.Contains("stock", problem?.Errors.Keys ?? []);
    }

    [Fact]
    public async Task CreateProduct_CreatesValidProduct()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            name = "USB-C Dock",
            description = "Docking station",
            price = 149.99m,
            stock = 7
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(product);
        Assert.Equal("USB-C Dock", product.Name);
        Assert.Equal(149.99m, product.Price);
        Assert.Equal(7, product.Stock);
        Assert.Equal($"/api/products/{product.Id}", response.Headers.Location?.OriginalString);
    }
}
