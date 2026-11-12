using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Portal.Application.Abstractions;
using Portal.Infrastructure.Options;

namespace Portal.Infrastructure.Auth;

public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly SigningKeyProvider _keys;
    private readonly JwtOptions _options;

    public JwtTokenIssuer(SigningKeyProvider keys, IOptions<JwtOptions> options)
    {
        _keys = keys;
        _options = options.Value;
    }

    public (string Token, int ExpiresInSeconds) IssueAccessToken(
        TokenSubject subject,
        Guid appId,
        string appSlug,
        string audience)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        // Dùng claim kiểu OIDC để app bên ngoài đọc trực tiếp, không phụ thuộc .NET claim mapping.
        var claims = new List<Claim>
        {
            new("sub", subject.UserId.ToString()),
            new("email", subject.Email),
            new("name", subject.Name),
            new("app_id", appId.ToString()),
            new("app_slug", appSlug),
            new("jti", Guid.NewGuid().ToString("N"))
        };

        foreach (var role in subject.Roles)
        {
            claims.Add(new Claim("role", role));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(
                _keys.SecurityKey, SecurityAlgorithms.RsaSha256)
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        return (token, (int)(expires - now).TotalSeconds);
    }
}
