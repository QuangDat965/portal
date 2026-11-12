using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Portal.Application.Abstractions;
using Portal.Application.Dtos;
using Portal.Domain.Entities;
using Portal.Infrastructure.Persistence;

namespace Portal.Infrastructure.Services;

public sealed class LaunchService : ILaunchService
{
    private const int LaunchCodeLifetimeSeconds = 60;

    private readonly PortalDbContext _db;
    private readonly IEntitlementService _entitlements;
    private readonly ISecretHasher _hasher;
    private readonly ITokenIssuer _tokens;

    public LaunchService(
        PortalDbContext db,
        IEntitlementService entitlements,
        ISecretHasher hasher,
        ITokenIssuer tokens)
    {
        _db = db;
        _entitlements = entitlements;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<LaunchResult?> CreateLaunchAsync(
        Guid userId,
        string slug,
        bool isAdmin,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var app = await _db.Apps.FirstOrDefaultAsync(
            a => a.Slug == slug && a.Status == AppStatus.Active, cancellationToken);

        if (app is null)
        {
            return null;
        }

        if (!await _entitlements.CanAccessAsync(userId, app.Id, isAdmin, cancellationToken))
        {
            return null;
        }

        var code = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;

        _db.LaunchCodes.Add(new LaunchCode
        {
            CodeHash = _hasher.Hash(code),
            UserId = userId,
            AppId = app.Id,
            ExpiresAt = now.AddSeconds(LaunchCodeLifetimeSeconds),
            CreatedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            AppId = app.Id,
            Action = "app.launch",
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        var path = app.CallbackPath.StartsWith('/') ? app.CallbackPath : "/" + app.CallbackPath;
        var redirectUrl = app.BaseUrl.TrimEnd('/') + path
                          + (path.Contains('?') ? "&" : "?")
                          + "code=" + Uri.EscapeDataString(code);

        return new LaunchResult(redirectUrl, LaunchCodeLifetimeSeconds);
    }

    public async Task<TokenResponse?> ExchangeAsync(
        string clientId,
        string clientSecret,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var client = await _db.AppClients
            .Include(c => c.App)
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

        if (client is null || !_hasher.Verify(client.ClientSecretHash, clientSecret))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var codeHash = _hasher.Hash(code);

        var launch = await _db.LaunchCodes.FirstOrDefaultAsync(
            lc => lc.CodeHash == codeHash
                  && lc.AppId == client.AppId
                  && lc.UsedAt == null
                  && lc.ExpiresAt > now,
            cancellationToken);

        if (launch is null)
        {
            return null;
        }

        // Đánh dấu đã dùng theo cách atomic, tránh 2 request cùng đổi được 1 code.
        var updated = await _db.LaunchCodes
            .Where(lc => lc.Id == launch.Id && lc.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(lc => lc.UsedAt, now), cancellationToken);

        if (updated == 0)
        {
            return null;
        }

        var user = await _db.Users.FindAsync([launch.UserId], cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = await (
                from ur in _db.UserRoles
                join r in _db.Roles on ur.RoleId equals r.Id
                where ur.UserId == user.Id
                select r.Name!)
            .ToListAsync(cancellationToken);

        var subject = new TokenSubject(
            user.Id,
            user.Email ?? user.UserName ?? string.Empty,
            string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.UserName ?? user.Email ?? "Người dùng"
                : user.DisplayName,
            roles);

        var (token, expiresIn) = _tokens.IssueAccessToken(
            subject, client.AppId, client.App.Slug, client.ClientId);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            AppId = client.AppId,
            Action = "app.token_issued",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new TokenResponse(
            token,
            "Bearer",
            expiresIn,
            new UserDto(
                user.Id,
                user.Email ?? string.Empty,
                subject.Name,
                user.AvatarUrl),
            roles);
    }
}
