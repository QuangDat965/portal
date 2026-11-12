using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Application.Abstractions;
using Portal.Domain;
using Portal.Domain.Entities;
using Portal.Infrastructure.Identity;
using Portal.Infrastructure.Options;

namespace Portal.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<PortalDbContext>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = provider.GetRequiredService<UserManager<AppUser>>();
        var hasher = provider.GetRequiredService<ISecretHasher>();
        var options = provider.GetRequiredService<IOptions<SeedOptions>>().Value;

        await db.Database.MigrateAsync(cancellationToken);

        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

        var admin = await EnsureAdminAsync(userManager, options, logger);
        if (admin is not null && !await userManager.IsInRoleAsync(admin, RoleNames.Admin))
        {
            await userManager.AddToRoleAsync(admin, RoleNames.Admin);
        }

        if (options.DemoData)
        {
            await EnsureDemoAppAsync(db, hasher, options, logger, cancellationToken);
        }

        logger.LogInformation("Portal database đã sẵn sàng.");
    }

    private static async Task<AppUser?> EnsureAdminAsync(
        UserManager<AppUser> userManager,
        SeedOptions options,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.AdminEmail))
        {
            return null;
        }

        var admin = await userManager.FindByEmailAsync(options.AdminEmail);
        if (admin is not null)
        {
            await EnsureAdminPasswordAsync(userManager, admin, options, logger);
            return admin;
        }

        admin = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = options.AdminEmail,
            Email = options.AdminEmail,
            EmailConfirmed = true,
            DisplayName = options.AdminName
        };

        var result = await userManager.CreateAsync(admin);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Không tạo được admin {Email}: {Errors}",
                options.AdminEmail,
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return null;
        }

        logger.LogInformation("Đã seed admin {Email}.", options.AdminEmail);
        await EnsureAdminPasswordAsync(userManager, admin, options, logger);
        return admin;
    }

    private static async Task EnsureAdminPasswordAsync(
        UserManager<AppUser> userManager,
        AppUser admin,
        SeedOptions options,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.AdminPassword)
            || await userManager.HasPasswordAsync(admin))
        {
            return;
        }

        var result = await userManager.AddPasswordAsync(admin, options.AdminPassword);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Không đặt được mật khẩu cho admin {Email}: {Errors}",
                admin.Email,
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("Đã đặt mật khẩu đăng nhập cho admin {Email}.", admin.Email);
    }

    private static async Task EnsureDemoAppAsync(
        PortalDbContext db,
        ISecretHasher hasher,
        SeedOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var memberRoleId = await db.Roles
            .Where(r => r.Name == RoleNames.Member)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (memberRoleId == Guid.Empty)
        {
            return;
        }

        var app = await db.Apps.FirstOrDefaultAsync(a => a.Slug == "demo", cancellationToken);
        if (app is null)
        {
            app = new RegisteredApp
            {
                Slug = "demo",
                Name = "Demo App",
                Description = "Ứng dụng mẫu để kiểm tra luồng đăng nhập một lần (SSO).",
                BaseUrl = options.DemoAppUrl,
                CallbackPath = "/auth/callback",
                Status = AppStatus.Active,
                SortOrder = 100
            };

            db.Apps.Add(app);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Đã seed app demo tại {Url}.", options.DemoAppUrl);
        }

        if (!await db.AppAccess.AnyAsync(
                x => x.AppId == app.Id && x.RoleId == memberRoleId, cancellationToken))
        {
            db.AppAccess.Add(new AppAccess { AppId = app.Id, RoleId = memberRoleId });
        }

        if (!await db.AppClients.AnyAsync(c => c.ClientId == "demo", cancellationToken))
        {
            db.AppClients.Add(new AppClient
            {
                AppId = app.Id,
                ClientId = "demo",
                ClientSecretHash = hasher.Hash(options.DemoAppClientSecret)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
