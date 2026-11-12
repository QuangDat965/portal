using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Portal.Domain.Entities;
using Portal.Infrastructure.Identity;

namespace Portal.Infrastructure.Persistence;

public class PortalDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options)
    {
    }

    public DbSet<RegisteredApp> Apps => Set<RegisteredApp>();
    public DbSet<AppAccess> AppAccess => Set<AppAccess>();
    public DbSet<AppClient> AppClients => Set<AppClient>();
    public DbSet<LaunchCode> LaunchCodes => Set<LaunchCode>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(b =>
        {
            b.Property(x => x.DisplayName).HasMaxLength(200);
            b.Property(x => x.AvatarUrl).HasMaxLength(1000);
        });

        builder.Entity<RegisteredApp>(b =>
        {
            b.ToTable("apps");
            b.HasKey(x => x.Id);
            b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.IconUrl).HasMaxLength(1000);
            b.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired();
            b.Property(x => x.CallbackPath).HasMaxLength(200).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<AppAccess>(b =>
        {
            b.ToTable("app_access");
            b.HasKey(x => new { x.AppId, x.RoleId });
            b.HasOne(x => x.App)
                .WithMany(x => x.Access)
                .HasForeignKey(x => x.AppId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.RoleId);
        });

        builder.Entity<AppClient>(b =>
        {
            b.ToTable("app_clients");
            b.HasKey(x => x.Id);
            b.Property(x => x.ClientId).HasMaxLength(100).IsRequired();
            b.Property(x => x.ClientSecretHash).HasMaxLength(200).IsRequired();
            b.HasIndex(x => x.ClientId).IsUnique();
            b.HasOne(x => x.App)
                .WithMany(x => x.Clients)
                .HasForeignKey(x => x.AppId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LaunchCode>(b =>
        {
            b.ToTable("launch_codes");
            b.HasKey(x => x.Id);
            b.Property(x => x.CodeHash).HasMaxLength(200).IsRequired();
            b.HasIndex(x => x.CodeHash).IsUnique();
            b.HasIndex(x => x.ExpiresAt);
            b.HasOne(x => x.App)
                .WithMany()
                .HasForeignKey(x => x.AppId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(b =>
        {
            b.ToTable("audit_logs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).HasMaxLength(100).IsRequired();
            b.Property(x => x.IpAddress).HasMaxLength(100);
            b.Property(x => x.UserAgent).HasMaxLength(500);
            b.HasIndex(x => x.CreatedAt);
        });
    }
}
