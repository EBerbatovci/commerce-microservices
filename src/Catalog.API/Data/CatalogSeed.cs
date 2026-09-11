using Catalog.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Data;

public static class CatalogSeed
{
    public static async Task InitializeAsync(CatalogDbContext database)
    {
        await database.Database.EnsureCreatedAsync();

        if (await database.Products.AnyAsync())
        {
            return;
        }

        database.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Wireless Headphones",
                Description = "Noise-cancelling headphones for focused work.",
                Price = 129.90m,
                Stock = 24,
                CreatedAtUtc = DateTimeOffset.UtcNow
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Mechanical Keyboard",
                Description = "Compact mechanical keyboard with tactile switches.",
                Price = 89.50m,
                Stock = 18,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

        await database.SaveChangesAsync();
    }
}

