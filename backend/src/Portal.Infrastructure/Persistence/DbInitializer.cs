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
            await EnsureDemoAppsAsync(db, hasher, options, logger, cancellationToken);
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

    private static async Task EnsureDemoAppsAsync(
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

        var seeds = new[]
        {
            new SeedApp(
                "demo",
                "Demo App",
                "Ứng dụng mẫu để kiểm tra luồng đăng nhập một lần (SSO).",
                options.DemoAppUrl,
                "demo",
                options.DemoAppClientSecret,
                100),
            new SeedApp(
                "tasks",
                "Taskboard",
                "Bảng công việc nhóm: thêm, sửa, đánh dấu hoàn thành và xoá task.",
                options.TasksAppUrl,
                "tasks",
                options.TasksClientSecret,
                110),
            new SeedApp(
                "notes",
                "Team Notes",
                "Ghi chú nội bộ: tạo, ghim, sửa và xoá ghi chú của nhóm.",
                options.NotesAppUrl,
                "notes",
                options.NotesClientSecret,
                120),
            new SeedApp(
                "exploding-kittens",
                "Mèo Nổ",
                "Chơi Mèo Nổ online cùng 2–5 người bạn.",
                options.KittensAppUrl,
                "exploding-kittens",
                options.KittensClientSecret,
                130)
        };

        foreach (var seed in seeds)
        {
            await EnsureAppAsync(db, hasher, logger, seed, memberRoleId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeedApp(
        string Slug,
        string Name,
        string Description,
        string BaseUrl,
        string ClientId,
        string ClientSecret,
        int SortOrder);

    private static async Task EnsureAppAsync(
        PortalDbContext db,
        ISecretHasher hasher,
        ILogger logger,
        SeedApp seed,
        Guid memberRoleId,
        CancellationToken cancellationToken)
    {
        var app = await db.Apps.FirstOrDefaultAsync(a => a.Slug == seed.Slug, cancellationToken);

        if (app is null)
        {
            app = new RegisteredApp
            {
                Slug = seed.Slug,
                Name = seed.Name,
                Description = seed.Description,
                BaseUrl = seed.BaseUrl,
                CallbackPath = "/auth/callback",
                Status = AppStatus.Active,
                SortOrder = seed.SortOrder
            };

            db.Apps.Add(app);
            logger.LogInformation("Đã seed app {Slug} tại {Url}.", seed.Slug, seed.BaseUrl);
        }
        else if (!string.Equals(app.BaseUrl, seed.BaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            app.BaseUrl = seed.BaseUrl;
            app.UpdatedAt = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "Đã cập nhật BaseUrl của app {Slug} thành {Url}.", seed.Slug, seed.BaseUrl);
        }

        if (!await db.AppAccess.AnyAsync(
                x => x.AppId == app.Id && x.RoleId == memberRoleId, cancellationToken))
        {
            db.AppAccess.Add(new AppAccess { AppId = app.Id, RoleId = memberRoleId });
        }

        if (!await db.AppClients.AnyAsync(c => c.ClientId == seed.ClientId, cancellationToken))
        {
            db.AppClients.Add(new AppClient
            {
                AppId = app.Id,
                ClientId = seed.ClientId,
                ClientSecretHash = hasher.Hash(seed.ClientSecret)
            });
        }
    }
}
