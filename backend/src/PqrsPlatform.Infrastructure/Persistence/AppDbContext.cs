using Microsoft.EntityFrameworkCore;
using PqrsPlatform.Domain.Entities;
using PqrsPlatform.Domain.Interfaces;

namespace PqrsPlatform.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles => Set<KnowledgeBaseArticle>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<RagInteraction> RagInteractions => Set<RagInteraction>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("vector");

        b.Entity<Tenant>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            e.Property(x => x.AllowedOrigins).HasColumnType("text[]");
        });

        b.Entity<User>(e =>
        {

            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasMaxLength(30).IsRequired();

            e.HasOne(x => x.Tenant).WithMany(t => t.Users)
             .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });

        b.Entity<KnowledgeBaseArticle>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Embedding).HasColumnType("vector(1536)");

            e.HasOne(x => x.Tenant).WithMany(t => t.Articles)
             .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.TenantId);
            
            e.HasIndex(x => x.Embedding)
             .HasMethod("hnsw")
             .HasOperators("vector_cosine_ops");

            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });
        
        b.Entity<Ticket>(e =>
        {
            e.Property(x => x.TicketNumber).HasMaxLength(40).IsRequired();
            e.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            e.Property(x => x.CustomerEmail).HasMaxLength(200).IsRequired();
            e.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Sentiment).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(x => x.Tenant).WithMany(t => t.Tickets)
             .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.AssignedTo).WithMany()
             .HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
            
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.Priority });
            e.HasIndex(x => new { x.TenantId, x.TicketNumber }).IsUnique();

            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });
        
        b.Entity<RagInteraction>(e =>
        {
            e.HasOne(x => x.Tenant).WithMany()
             .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Ticket).WithMany()
             .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.TenantId, x.CreatedAt });

            e.HasQueryFilter(x => x.TenantId == _tenant.TenantId);
        });
    }
    
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added) continue;

            var prop = entry.Metadata.FindProperty("TenantId");
            if (prop is null) continue;

            var current = entry.Property("TenantId").CurrentValue;
            if (current is Guid g && g == Guid.Empty)
                entry.Property("TenantId").CurrentValue = _tenant.TenantId;
        }

        return base.SaveChangesAsync(ct);
    }
}