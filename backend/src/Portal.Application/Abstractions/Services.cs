using Portal.Application.Dtos;

namespace Portal.Application.Abstractions;

public sealed record TokenSubject(
    Guid UserId,
    string Email,
    string Name,
    IReadOnlyList<string> Roles);

public interface IEntitlementService
{
    Task<IReadOnlyList<AppDto>> GetAppsForUserAsync(
        Guid userId, bool isAdmin, CancellationToken cancellationToken);

    Task<bool> CanAccessAsync(
        Guid userId, Guid appId, bool isAdmin, CancellationToken cancellationToken);
}

public interface ILaunchService
{
    Task<LaunchResult?> CreateLaunchAsync(
        Guid userId,
        string slug,
        bool isAdmin,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);

    Task<TokenResponse?> ExchangeAsync(
        string clientId,
        string clientSecret,
        string code,
        CancellationToken cancellationToken);
}

public interface ISecretHasher
{
    string Hash(string value);
    bool Verify(string hash, string value);
}

public interface ITokenIssuer
{
    (string Token, int ExpiresInSeconds) IssueAccessToken(
        TokenSubject subject,
        Guid appId,
        string appSlug,
        string audience);
}

public interface ISigningKeyProvider
{
    string KeyId { get; }
    object GetJwksDocument();
}
