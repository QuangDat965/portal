using Microsoft.EntityFrameworkCore;
using Portal.Application.Abstractions;
using Portal.Application.Dtos;
using Portal.Domain.Entities;
using Portal.Infrastructure.Persistence;

namespace Portal.Infrastructure.Services;

public sealed class EntitlementService : IEntitlementService
{
    private readonly PortalDbContext _db;

    public EntitlementService(PortalDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AppDto>> GetAppsForUserAsync(
        Guid userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var query = _db.Apps.AsNoTracking()
            .Where(a => a.Status == AppStatus.Active);

        if (!isAdmin)
        {
            var roleIds = _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId);

            query = query.Where(a => a.Access.Any(x => roleIds.Contains(x.RoleId)));
        }

        var apps = await query
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        return apps.Select(ToDto).ToList();
    }

    public async Task<bool> CanAccessAsync(
        Guid userId, Guid appId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (isAdmin)
        {
            return await _db.Apps.AnyAsync(
                a => a.Id == appId && a.Status == AppStatus.Active, cancellationToken);
        }

        var roleIds = _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId);

        return await _db.Apps.AnyAsync(
            a => a.Id == appId
                 && a.Status == AppStatus.Active
                 && a.Access.Any(x => roleIds.Contains(x.RoleId)),
            cancellationToken);
    }

    internal static AppDto ToDto(RegisteredApp app) => new(
        app.Id,
        app.Slug,
        app.Name,
        app.Description,
        app.IconUrl,
        app.BaseUrl,
        app.Status.ToString(),
        app.SortOrder);
}
