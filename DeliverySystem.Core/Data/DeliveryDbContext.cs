
using Microsoft.EntityFrameworkCore;


namespace DeliverySystem.Core.Data;

public class DeliveryDbContext : DbContext
{
    public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : base(options) { }

    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderHistoryEvent> OrderHistoryEvents { get; set; }
    public DbSet<OutboxEvent> OutboxEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>()
            .Property(o => o.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<OutboxEvent>()
            .HasIndex(e => new { e.IsProcessed, e.CreatedAt });
    }
}