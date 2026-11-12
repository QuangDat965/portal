using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Portal.Api.Auth;
using Portal.Api.Contracts;
using Portal.Application.Abstractions;
using Portal.Application.Dtos;
using Portal.Domain;
using Portal.Domain.Entities;
using Portal.Infrastructure.Identity;
using Portal.Infrastructure.Persistence;

namespace Portal.Api.Endpoints;

public static partial class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin");

        group.MapGet("/apps", async (PortalDbContext db, CancellationToken cancellationToken) =>
        {
            var apps = await db.Apps.AsNoTracking()
                .Include(a => a.Access)
                .Include(a => a.Clients)
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Name)
                .ToListAsync(cancellationToken);

            return Results.Ok(apps.Select(ToAdminDto));
        });

        group.MapPost("/apps", async (
            AppUpsertRequest request,
            HttpContext context,
            ClaimsPrincipal user,
            PortalDbContext db,
            ISecretHasher hasher,
            CancellationToken cancellationToken) =>
        {
            if (await context.ValidateAntiforgeryAsync() is { } csrfError)
            {
                return csrfError;
            }

            var validation = ValidateAppRequest(request);
            if (validation is not null)
            {
                return Results.BadRequest(new { error = validation });
            }

            var slug = request.Slug.Trim().ToLowerInvariant();
            if (await db.Apps.AnyAsync(a => a.Slug == slug, cancellationToken))
            {
                return Results.Conflict(new { error = "Slug đã tồn tại." });
            }

            var roleIds = await GetValidRoleIdsAsync(db, request.RoleIds, cancellationToken);
            var app = new RegisteredApp
            {
                Slug = slug,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                IconUrl = request.IconUrl?.Trim(),
                BaseUrl = request.BaseUrl.Trim().TrimEnd('/'),
                CallbackPath = NormalizeCallbackPath(request.CallbackPath),
                Status = ParseStatus(request.Status),
                SortOrder = request.SortOrder
            };

            foreach (var roleId in roleIds)
            {
                app.Access.Add(new AppAccess { RoleId = roleId });
            }

            var clientSecret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            app.Clients.Add(new AppClient
            {
                ClientId = slug,
                ClientSecretHash = hasher.Hash(clientSecret)
            });

            db.Apps.Add(app);
            AddAudit(db, user, context, app.Id, "admin.app_created", slug);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/admin/apps/{app.Id}",
                new CreateAppResponse(ToAdminDto(app), clientSecret));
        });

        group.MapPut("/apps/{id:guid}", async (
            Guid id,
            AppUpsertRequest request,
            HttpContext context,
            ClaimsPrincipal user,
            PortalDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (await context.ValidateAntiforgeryAsync() is { } csrfError)
            {
                return csrfError;
            }

            var validation = ValidateAppRequest(request);
            if (validation is not null)
            {
                return Results.BadRequest(new { error = validation });
            }

            var app = await db.Apps
                .Include(a => a.Access)
                .Include(a => a.Clients)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (app is null)
            {
                return Results.NotFound();
            }

            var slug = request.Slug.Trim().ToLowerInvariant();
            if (await db.Apps.AnyAsync(a => a.Slug == slug && a.Id != id, cancellationToken))
            {
                return Results.Conflict(new { error = "Slug đã tồn tại." });
            }

            app.Slug = slug;
            app.Name = request.Name.Trim();
            app.Description = request.Description?.Trim();
            app.IconUrl = request.IconUrl?.Trim();
            app.BaseUrl = request.BaseUrl.Trim().TrimEnd('/');
            app.CallbackPath = NormalizeCallbackPath(request.CallbackPath);
            app.Status = ParseStatus(request.Status);
            app.SortOrder = request.SortOrder;
            app.UpdatedAt = DateTimeOffset.UtcNow;

            app.Access.Clear();
            var roleIds = await GetValidRoleIdsAsync(db, request.RoleIds, cancellationToken);
            foreach (var roleId in roleIds)
            {
                app.Access.Add(new AppAccess { AppId = app.Id, RoleId = roleId });
            }

            AddAudit(db, user, context, app.Id, "admin.app_updated", slug);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToAdminDto(app));
        });

        group.MapDelete("/apps/{id:guid}", async (
            Guid id,
            HttpContext context,
            ClaimsPrincipal user,
            PortalDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (await context.ValidateAntiforgeryAsync() is { } csrfError)
            {
                return csrfError;
            }

            var app = await db.Apps.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
            if (app is null)
            {
                return Results.NotFound();
            }

            db.Apps.Remove(app);
            AddAudit(db, user, context, null, "admin.app_deleted", app.Slug);
            await db.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        group.MapPost("/apps/{id:guid}/client/rotate", async (
            Guid id,
            HttpContext context,
            ClaimsPrincipal user,
            PortalDbContext db,
            ISecretHasher hasher,
            CancellationToken cancellationToken) =>
        {
            if (await context.ValidateAntiforgeryAsync() is { } csrfError)
            {
                return csrfError;
            }

            var app = await db.Apps
                .Include(a => a.Clients)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (app is null)
            {
                return Results.NotFound();
            }

            var client = app.Clients.FirstOrDefault();
            var clientId = client?.ClientId ?? app.Slug;
            var secret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            var now = DateTimeOffset.UtcNow;

            if (client is null)
            {
                db.AppClients.Add(new AppClient
                {
                    AppId = app.Id,
                    ClientId = clientId,
                    ClientSecretHash = hasher.Hash(secret),
                    CreatedAt = now,
                    RotatedAt = now
                });
            }
            else
            {
                client.ClientSecretHash = hasher.Hash(secret);
                client.RotatedAt = now;
            }

            AddAudit(db, user, context, app.Id, "admin.client_rotated", clientId);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new RotateClientResponse(clientId, secret));
        });

        group.MapGet("/roles", async (PortalDbContext db, CancellationToken cancellationToken) =>
        {
            var roles = await db.Roles.AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new RoleDto(r.Id, r.Name!))
                .ToListAsync(cancellationToken);

            return Results.Ok(roles);
        });

        group.MapGet("/users", async (
            string? search,
            PortalDbContext db,
            CancellationToken cancellationToken) =>
        {
            var query = db.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u =>
                    (u.Email != null && u.Email.Contains(term)) ||
                    u.DisplayName.Contains(term));
            }

            var users = await query
                .OrderBy(u => u.Email)
                .Take(200)
                .ToListAsync(cancellationToken);

            return Results.Ok(await ToAdminUsersAsync(db, users, cancellationToken));
        });

        group.MapPut("/users/{id:guid}/roles", async (
            Guid id,
            UpdateUserRolesRequest request,
            HttpContext context,
            ClaimsPrincipal currentUser,
            UserManager<AppUser> userManager,
            PortalDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (await context.ValidateAntiforgeryAsync() is { } csrfError)
            {
                return csrfError;
            }

            var target = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (target is null)
            {
                return Results.NotFound();
            }

            var roleNames = (request.Roles ?? [])
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            var existingRoles = await db.Roles
                .Where(r => roleNames.Contains(r.Name!))
                .Select(r => r.Name!)
                .ToListAsync(cancellationToken);

            if (existingRoles.Count != roleNames.Count)
            {
                return Results.BadRequest(new { error = "Có role không tồn tại." });
            }

            if (id == currentUser.GetUserId() && !roleNames.Contains(RoleNames.Admin))
            {
                return Results.BadRequest(new { error = "Không thể tự bỏ quyền admin." });
            }

            if (await userManager.IsInRoleAsync(target, RoleNames.Admin)
                && !roleNames.Contains(RoleNames.Admin))
            {
                var admins = await userManager.GetUsersInRoleAsync(RoleNames.Admin);
                if (admins.Count <= 1)
                {
                    return Results.BadRequest(new { error = "Không thể bỏ admin cuối cùng." });
                }
            }

            var currentRoles = await userManager.GetRolesAsync(target);
            var toAdd = roleNames.Except(currentRoles).ToList();
            var toRemove = currentRoles.Except(roleNames).ToList();

            if (toAdd.Count > 0)
            {
                await userManager.AddToRolesAsync(target, toAdd);
            }

            if (toRemove.Count > 0)
            {
                await userManager.RemoveFromRolesAsync(target, toRemove);
            }

            AddAudit(db, currentUser, context, null, "admin.user_roles_updated",
                $"{target.Email}:{string.Join(',', roleNames)}");
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok((await ToAdminUsersAsync(db, [target], cancellationToken)).Single());
        });

        group.MapPut("/users/{id:guid}/password", async (
            Guid id,
            SetUserPasswordRequest request,
            HttpContext context,
            ClaimsPrincipal currentUser,
            UserManager<AppUser> userManager,
            PortalDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (await context.ValidateAntiforgeryAsync() is { } csrfError)
            {
                return csrfError;
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { error = "Mật khẩu không được để trống." });
            }

            var target = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (target is null)
            {
                return Results.NotFound();
            }

            var token = await userManager.GeneratePasswordResetTokenAsync(target);
            var result = await userManager.ResetPasswordAsync(target, token, request.Password);

            if (!result.Succeeded)
            {
                return Results.BadRequest(new
                {
                    error = string.Join("; ", result.Errors.Select(e => e.Description))
                });
            }

            AddAudit(db, currentUser, context, null, "admin.user_password_reset",
                target.Email);
            await db.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        group.MapGet("/audit", async (
            int? take,
            PortalDbContext db,
            CancellationToken cancellationToken) =>
        {
            var limit = Math.Clamp(take ?? 100, 1, 200);
            var logs = await db.AuditLogs.AsNoTracking()
                .OrderByDescending(l => l.CreatedAt)
                .Take(limit)
                .Select(l => new AuditLogDto(
                    l.Id, l.UserId, l.AppId, l.Action, l.IpAddress, l.CreatedAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(logs);
        });

        return app;
    }

    private static AdminAppDto ToAdminDto(RegisteredApp app) => new(
        app.Id,
        app.Slug,
        app.Name,
        app.Description,
        app.IconUrl,
        app.BaseUrl,
        app.CallbackPath,
        app.Status.ToString(),
        app.SortOrder,
        app.Access.Select(a => a.RoleId).ToList(),
        app.Clients.OrderBy(c => c.CreatedAt).FirstOrDefault()?.ClientId);

    private static async Task<IReadOnlyList<AdminUserDto>> ToAdminUsersAsync(
        PortalDbContext db,
        IReadOnlyCollection<AppUser> users,
        CancellationToken cancellationToken)
    {
        var userIds = users.Select(u => u.Id).ToList();
        var roleRows = await (
                from ur in db.UserRoles
                join r in db.Roles on ur.RoleId equals r.Id
                where userIds.Contains(ur.UserId)
                select new { ur.UserId, Name = r.Name! })
            .ToListAsync(cancellationToken);

        var rolesByUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).OrderBy(n => n).ToList());

        return users.Select(u => new AdminUserDto(
            u.Id,
            u.Email ?? string.Empty,
            string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName ?? string.Empty : u.DisplayName,
            u.AvatarUrl,
            rolesByUser.GetValueOrDefault(u.Id, []),
            u.CreatedAt,
            u.LastLoginAt)).ToList();
    }

    private static async Task<List<Guid>> GetValidRoleIdsAsync(
        PortalDbContext db,
        IReadOnlyList<Guid>? roleIds,
        CancellationToken cancellationToken)
    {
        if (roleIds is null || roleIds.Count == 0)
        {
            return [];
        }

        var ids = roleIds.Distinct().ToList();
        return await db.Roles
            .Where(r => ids.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    private static void AddAudit(
        PortalDbContext db,
        ClaimsPrincipal user,
        HttpContext context,
        Guid? appId,
        string action,
        string? metadata)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = user.GetUserId(),
            AppId = appId,
            Action = action,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            Metadata = metadata
        });
    }

    private static string? ValidateAppRequest(AppUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Slug) || !SlugRegex().IsMatch(request.Slug.Trim()))
        {
            return "Slug chỉ gồm chữ thường, số và dấu gạch ngang (2-80 ký tự).";
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
        {
            return "Tên app không hợp lệ.";
        }

        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return "BaseUrl phải là URL http/https tuyệt đối.";
        }

        if (!string.IsNullOrWhiteSpace(request.CallbackPath)
            && (!request.CallbackPath.StartsWith('/') || request.CallbackPath.Contains(' ')))
        {
            return "CallbackPath phải bắt đầu bằng '/' và không chứa khoảng trắng.";
        }

        return null;
    }

    private static string NormalizeCallbackPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? "/auth/callback" : path.Trim();

    private static AppStatus ParseStatus(string? status) =>
        Enum.TryParse<AppStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : AppStatus.Active;

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,78}[a-z0-9])?$")]
    private static partial Regex SlugRegex();
}
