using Microsoft.EntityFrameworkCore;

namespace SammiCrawl.Backend.Data;

public class Store(DbContextOptions<Store> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>(); public DbSet<ProjectUser> Users => Set<ProjectUser>(); public DbSet<Audit> Logs => Set<Audit>();
    protected override void OnModelCreating(ModelBuilder b) { if (Database.IsNpgsql()) b.HasDefaultSchema("sammicrawl"); b.Entity<ProjectUser>().HasIndex(x => x.Email).IsUnique(); b.Entity<Document>().HasIndex(x => new { x.Kind, x.CreatedAt }); }
}
