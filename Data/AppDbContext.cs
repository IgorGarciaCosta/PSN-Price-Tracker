using Microsoft.EntityFrameworkCore;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Data;

public class AppDbContext : DbContext
{
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKeyEntity>(entity =>
        {
            entity.HasIndex(e => e.Chave).IsUnique();
            entity.Property(e => e.Chave).IsRequired().HasMaxLength(64);
        });
    }
}
