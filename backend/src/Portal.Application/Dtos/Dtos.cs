namespace Portal.Application.Dtos;

public sealed record UserDto(
    Guid Id,
    string Email,
    string Name,
    string? AvatarUrl);

public sealed record MeDto(
    Guid Id,
    string Email,
    string Name,
    string? AvatarUrl,
    IReadOnlyList<string> Roles);

public sealed record AppDto(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    string? IconUrl,
    string BaseUrl,
    string Status,
    int SortOrder);

public sealed record LaunchResult(
    string RedirectUrl,
    int ExpiresInSeconds);

public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    UserDto User,
    IReadOnlyList<string> Roles);

public sealed record RoleDto(Guid Id, string Name);

public sealed record AdminAppDto(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    string? IconUrl,
    string BaseUrl,
    string CallbackPath,
    string Status,
    int SortOrder,
    IReadOnlyList<Guid> RoleIds,
    string? ClientId);
