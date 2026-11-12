using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portal.Application.Abstractions;
using Portal.Infrastructure.Auth;
using Portal.Infrastructure.Options;
using Portal.Infrastructure.Persistence;
using Portal.Infrastructure.Security;
using Portal.Infrastructure.Services;

namespace Portal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPortalInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Thiếu ConnectionStrings:Postgres trong cấu hình.");

        services.AddDbContext<PortalDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<AuthOptions>(configuration.GetSection("Auth"));
        services.Configure<SeedOptions>(configuration.GetSection("Seed"));
        services.Configure<SecurityOptions>(configuration.GetSection("Security"));

        services.AddSingleton<SigningKeyProvider>();
        services.AddSingleton<ISigningKeyProvider>(
            sp => sp.GetRequiredService<SigningKeyProvider>());

        services.AddScoped<ISecretHasher, SecretHasher>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<ILaunchService, LaunchService>();

        return services;
    }
}
