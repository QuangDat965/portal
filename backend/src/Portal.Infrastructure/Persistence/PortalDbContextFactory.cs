using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Portal.Infrastructure.Persistence;

/// <summary>
/// Dùng cho `dotnet ef migrations` khi không chạy API.
/// </summary>
public sealed class PortalDbContextFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=portal;Username=portal;Password=portal";

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PortalDbContext(options);
    }
}
