namespace Catalog.API.Models;

public sealed class Product
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

