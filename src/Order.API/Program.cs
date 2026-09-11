using System.Net.Mail;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Order.API.Clients;
using Order.API.Contracts;
using Order.API.Data;
using Order.API.Errors;
using Order.API.Health;
using Order.API.Middleware;
using Order.API.Models;
using Order.API.OpenApi;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Commerce Order API",
        Version = "v1",
        Description = "Creates and manages customer orders with resilient product validation against the Catalog API.",
        Contact = new OpenApiContact { Name = "Endrina Berbatovci" }
    });
    options.OperationFilter<CorrelationIdResponseHeaderOperationFilter>();
});
builder.Services.AddHealthChecks()
    .AddCheck(
        "application",
        () => HealthCheckResult.Healthy("Order API is running."),
        tags: ["live", "ready"])
    .AddCheck<OrderDatabaseHealthCheck>("sqlite", tags: ["ready"])
    .AddCheck<CatalogApiHealthCheck>("catalog", tags: ["ready"]);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("OrderDatabase")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddHttpClient<CatalogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Catalog"]
        ?? throw new InvalidOperationException("Services:Catalog is not configured."));
    client.Timeout = Timeout.InfiniteTimeSpan;
})
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.Retry.UseJitter = true;
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 4;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    });

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

var readinessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = (context, report) =>
        HealthResponseWriter.WriteAsync(context, report, "Order.API")
};
var livenessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = (context, report) =>
        HealthResponseWriter.WriteAsync(context, report, "Order.API")
};

app.MapHealthChecks("/health", readinessOptions);
app.MapHealthChecks("/health/live", livenessOptions);
app.MapHealthChecks("/health/ready", readinessOptions);

var orders = app.MapGroup("/api/orders").WithTags("Orders");

orders.MapGet("/", async (OrderDbContext database, CancellationToken cancellationToken) =>
{
    var savedOrders = await database.Orders
        .AsNoTracking()
        .Include(order => order.Items)
        .ToListAsync(cancellationToken);

    savedOrders.Sort((left, right) => right.CreatedAtUtc.CompareTo(left.CreatedAtUtc));

    return Results.Ok(savedOrders);
})
    .WithSummary("List orders")
    .WithDescription("Returns all saved orders and their items, newest first.")
    .Produces<List<CustomerOrder>>(StatusCodes.Status200OK);

orders.MapGet("/{id:guid}", async (
    Guid id,
    OrderDbContext database,
    CancellationToken cancellationToken) =>
{
    var order = await database.Orders
        .AsNoTracking()
        .Include(item => item.Items)
        .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    return order is null
        ? Results.Problem(title: "Order not found.", statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(order);
})
    .WithSummary("Get an order")
    .WithDescription("Returns one order and its item snapshots by identifier.")
    .Produces<CustomerOrder>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);

orders.MapPost("/", async (
    CreateOrderRequest request,
    OrderDbContext database,
    CatalogClient catalogClient,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateOrder(request);
    if (validationErrors is not null)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var catalogProducts = new Dictionary<Guid, CatalogProduct>();
    var catalogValidationErrors = new List<string>();

    try
    {
        foreach (var itemGroup in request.Items.GroupBy(item => item.ProductId))
        {
            var product = await catalogClient.GetProductAsync(itemGroup.Key, cancellationToken);
            if (product is null)
            {
                catalogValidationErrors.Add($"Product '{itemGroup.Key}' does not exist.");
                continue;
            }

            catalogProducts[itemGroup.Key] = product;

            var requestedQuantity = itemGroup.Sum(item => (long)item.Quantity);
            if (requestedQuantity > product.Stock)
            {
                catalogValidationErrors.Add(
                    $"Product '{itemGroup.Key}' has insufficient stock. " +
                    $"Requested {requestedQuantity}, available {product.Stock}.");
            }
        }
    }
    catch (CatalogUnavailableException)
    {
        return Results.Problem(
            title: "Catalog API is unavailable.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (catalogValidationErrors.Count > 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Items)] = catalogValidationErrors.ToArray()
        });
    }

    var order = new CustomerOrder
    {
        Id = Guid.NewGuid(),
        CustomerEmail = request.CustomerEmail.Trim(),
        Status = OrderStatus.Pending,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        Items = request.Items.Select(item => new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = item.ProductId,
            ProductName = catalogProducts[item.ProductId].Name,
            UnitPrice = catalogProducts[item.ProductId].Price,
            Quantity = item.Quantity
        }).ToList()
    };

    order.Total = order.Items.Sum(item => item.LineTotal);

    database.Orders.Add(order);
    await database.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/orders/{order.Id}", order);
})
    .WithSummary("Create an order")
    .WithDescription("Validates products and stock with Catalog, snapshots current names and prices, and creates an order.")
    .Produces<CustomerOrder>(StatusCodes.Status201Created)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

orders.MapPatch("/{id:guid}/status", async (
    Guid id,
    UpdateOrderStatusRequest request,
    OrderDbContext database,
    CancellationToken cancellationToken) =>
{
    if (!Enum.IsDefined(request.Status))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Status)] = ["The requested order status is not valid."]
        });
    }

    var order = await database.Orders.FindAsync(new object[] { id }, cancellationToken);
    if (order is null)
    {
        return Results.Problem(
            title: "Order not found.",
            statusCode: StatusCodes.Status404NotFound);
    }

    order.Status = request.Status;
    await database.SaveChangesAsync(cancellationToken);

    return Results.Ok(order);
})
    .WithSummary("Update order status")
    .WithDescription("Changes the lifecycle status of an existing order.")
    .Produces<CustomerOrder>(StatusCodes.Status200OK)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound);

orders.MapDelete("/{id:guid}", async (
    Guid id,
    OrderDbContext database,
    CancellationToken cancellationToken) =>
{
    var order = await database.Orders.FindAsync(new object[] { id }, cancellationToken);
    if (order is null)
    {
        return Results.Problem(
            title: "Order not found.",
            statusCode: StatusCodes.Status404NotFound);
    }

    database.Orders.Remove(order);
    await database.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
})
    .WithSummary("Delete an order")
    .WithDescription("Permanently removes an order and its items.")
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound);

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await database.Database.EnsureCreatedAsync();
}

await app.RunAsync();

static Dictionary<string, string[]>? ValidateOrder(CreateOrderRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.CustomerEmail) || !IsValidEmail(request.CustomerEmail))
    {
        errors[nameof(request.CustomerEmail)] = ["A valid customer email is required."];
    }

    if (request.Items is null || request.Items.Count == 0)
    {
        errors[nameof(request.Items)] = ["An order must contain at least one item."];
        return errors;
    }

    if (request.Items.Any(item => item.ProductId == Guid.Empty))
    {
        errors["ProductId"] = ["Every item must reference a product."];
    }

    if (request.Items.Any(item => item.Quantity <= 0))
    {
        errors["Quantity"] = ["Every quantity must be greater than zero."];
    }

    return errors.Count == 0 ? null : errors;
}

static bool IsValidEmail(string value)
{
    try
    {
        _ = new MailAddress(value);
        return true;
    }
    catch (FormatException)
    {
        return false;
    }
}

public partial class Program;
