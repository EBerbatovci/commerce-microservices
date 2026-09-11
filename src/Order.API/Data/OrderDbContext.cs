using Microsoft.EntityFrameworkCore;
using Order.API.Models;

namespace Order.API.Data;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options)
    : DbContext(options)
{
    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerOrder>(entity =>
        {
            entity.HasKey(order => order.Id);
            entity.Property(order => order.CustomerEmail).HasMaxLength(254).IsRequired();
            entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(order => order.Total).HasPrecision(10, 2);
            entity.HasMany(order => order.Items)
                .WithOne()
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ProductName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(10, 2);
            entity.Ignore(item => item.LineTotal);
        });
    }
}
