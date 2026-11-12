using Portal.Application.Dtos;

namespace Portal.Api.Contracts;

public sealed record DevLoginRequest(string Email, string? Name);

public sealed record ExchangeTokenRequest(string ClientId, string ClientSecret, string Code);

public sealed record AppUpsertRequest(
    string Slug,
    string Name,
    string? Description,
    string? IconUrl,
    string BaseUrl,
    string? CallbackPath,
    string? Status,
    int SortOrder,
    IReadOnlyList<Guid>? RoleIds);

public sealed record CreateAppResponse(AdminAppDto App, string? ClientSecret);

public sealed record RotateClientResponse(string ClientId, string ClientSecret);

public sealed record UpdateUserRolesRequest(IReadOnlyList<string> Roles);

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string Name,
    string? AvatarUrl,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record AuditLogDto(
    long Id,
    Guid? UserId,
    Guid? AppId,
    string Action,
    string? IpAddress,
    DateTimeOffset CreatedAt);
