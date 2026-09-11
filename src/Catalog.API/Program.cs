using Catalog.API.Contracts;
using Catalog.API.Data;
using Catalog.API.Errors;
using Catalog.API.Health;
using Catalog.API.Middleware;
using Catalog.API.Models;
using Catalog.API.OpenApi;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Commerce Catalog API",
        Version = "v1",
        Description = "Manages the Commerce Microservices product catalog, including product details, pricing, and stock.",
        Contact = new OpenApiContact { Name = "Endrina Berbatovci" }
    });
    options.OperationFilter<CorrelationIdResponseHeaderOperationFilter>();
});
builder.Services.AddHealthChecks()
    .AddCheck(
        "application",
        () => HealthCheckResult.Healthy("Catalog API is running."),
        tags: ["live", "ready"])
    .AddCheck<CatalogDatabaseHealthCheck>("sqlite", tags: ["ready"]);
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("CatalogDatabase")));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

var readinessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = (context, report) =>
        HealthResponseWriter.WriteAsync(context, report, "Catalog.API")
};
var livenessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = (context, report) =>
        HealthResponseWriter.WriteAsync(context, report, "Catalog.API")
};

app.MapHealthChecks("/health", readinessOptions);
app.MapHealthChecks("/health/live", livenessOptions);
app.MapHealthChecks("/health/ready", readinessOptions);

var products = app.MapGroup("/api/products").WithTags("Products");

products.MapGet("/", async (CatalogDbContext database, CancellationToken cancellationToken) =>
    Results.Ok(await database.Products
        .AsNoTracking()
        .OrderBy(product => product.Name)
        .ToListAsync(cancellationToken)))
    .WithSummary("List products")
    .WithDescription("Returns every catalog product ordered by name.")
    .Produces<List<Product>>(StatusCodes.Status200OK);

products.MapGet("/{id:guid}", async (
    Guid id,
    CatalogDbContext database,
    CancellationToken cancellationToken) =>
{
    var product = await database.Products
        .AsNoTracking()
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    return product is null
        ? Results.Problem(title: "Product not found.", statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(product);
})
    .WithSummary("Get a product")
    .WithDescription("Returns one catalog product by its identifier.")
    .Produces<Product>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);

products.MapPost("/", async (
    CreateProductRequest request,
    CatalogDbContext database,
    CancellationToken cancellationToken) =>
{
    var validationError = ValidateProduct(request.Name, request.Price, request.Stock);
    if (validationError is not null)
    {
        return Results.ValidationProblem(validationError);
    }

    var product = new Product
    {
        Id = Guid.NewGuid(),
        Name = request.Name.Trim(),
        Description = request.Description?.Trim() ?? string.Empty,
        Price = request.Price,
        Stock = request.Stock,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    database.Products.Add(product);
    await database.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/products/{product.Id}", product);
})
    .WithSummary("Create a product")
    .WithDescription("Creates a catalog product after validating its name, price, and stock.")
    .Produces<Product>(StatusCodes.Status201Created)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest);

products.MapPut("/{id:guid}", async (
    Guid id,
    UpdateProductRequest request,
    CatalogDbContext database,
    CancellationToken cancellationToken) =>
{
    var validationError = ValidateProduct(request.Name, request.Price, request.Stock);
    if (validationError is not null)
    {
        return Results.ValidationProblem(validationError);
    }

    var product = await database.Products.FindAsync(new object[] { id }, cancellationToken);
    if (product is null)
    {
        return Results.Problem(
            title: "Product not found.",
            statusCode: StatusCodes.Status404NotFound);
    }

    product.Name = request.Name.Trim();
    product.Description = request.Description?.Trim() ?? string.Empty;
    product.Price = request.Price;
    product.Stock = request.Stock;

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(product);
})
    .WithSummary("Update a product")
    .WithDescription("Replaces the editable details of an existing catalog product.")
    .Produces<Product>(StatusCodes.Status200OK)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound);

products.MapDelete("/{id:guid}", async (
    Guid id,
    CatalogDbContext database,
    CancellationToken cancellationToken) =>
{
    var product = await database.Products.FindAsync(new object[] { id }, cancellationToken);
    if (product is null)
    {
        return Results.Problem(
            title: "Product not found.",
            statusCode: StatusCodes.Status404NotFound);
    }

    database.Products.Remove(product);
    await database.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
})
    .WithSummary("Delete a product")
    .WithDescription("Permanently removes a catalog product.")
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound);

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await CatalogSeed.InitializeAsync(database);
}

await app.RunAsync();

static Dictionary<string, string[]>? ValidateProduct(string name, decimal price, int stock)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(name))
    {
        errors[nameof(name)] = ["Product name is required."];
    }

    if (price <= 0)
    {
        errors[nameof(price)] = ["Price must be greater than zero."];
    }

    if (stock < 0)
    {
        errors[nameof(stock)] = ["Stock cannot be negative."];
    }

    return errors.Count == 0 ? null : errors;
}

public partial class Program;
