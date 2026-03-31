using Microsoft.EntityFrameworkCore;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Data;

public class AppDbContext : DbContext
{
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<AlertaEntity> Alertas => Set<AlertaEntity>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKeyEntity>(entity =>
        {
            entity.HasIndex(e => e.Chave).IsUnique();
            entity.Property(e => e.Chave).IsRequired().HasMaxLength(64);
        });

        modelBuilder.Entity<AlertaEntity>(entity =>
        {
            entity.HasIndex(e => new { e.TelegramChatId, e.UrlDoJogo, e.Ativo });
            entity.Property(e => e.UrlDoJogo).IsRequired().HasMaxLength(500);
            entity.Property(e => e.NomeDoJogo).IsRequired().HasMaxLength(300);
        });
    }
}
