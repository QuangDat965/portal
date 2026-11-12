using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Portal.Application.Abstractions;
using Portal.Infrastructure.Auth;
using Portal.Infrastructure.Options;

namespace Portal.Tests;

public class JwtTokenIssuerTests
{
    private static (SigningKeyProvider Provider, JwtTokenIssuer Issuer) CreateIssuer(
        string issuer = "https://issuer.test")
    {
        var keyPath = Path.Combine(
            Path.GetTempPath(),
            "portal-tests",
            Guid.NewGuid().ToString("N"),
            "jwt-private.pem");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivateKeyPath"] = keyPath
            })
            .Build();

        var provider = new SigningKeyProvider(
            configuration, NullLogger<SigningKeyProvider>.Instance);

        var issuerService = new JwtTokenIssuer(
            provider,
            Options.Create(new JwtOptions
            {
                Issuer = issuer,
                AccessTokenMinutes = 15
            }));

        return (provider, issuerService);
    }

    [Fact]
    public async Task Issues_Valid_Rs256_Token_With_Expected_Claims()
    {
        var (provider, issuer) = CreateIssuer();
        using var _ = provider;

        var userId = Guid.NewGuid();
        var appId = Guid.NewGuid();
        var subject = new TokenSubject(
            userId, "alice@example.com", "Alice", ["admin", "member"]);

        var (token, expiresIn) = issuer.IssueAccessToken(
            subject, appId, "demo", "demo-client");

        Assert.InRange(expiresIn, 890, 900);

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "https://issuer.test",
            ValidAudience = "demo-client",
            IssuerSigningKey = provider.SecurityKey,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        });

        Assert.True(result.IsValid);
        Assert.Equal(userId.ToString(), result.Claims["sub"]);
        Assert.Equal("alice@example.com", result.Claims["email"]);
        Assert.Equal("Alice", result.Claims["name"]);
        Assert.Equal("demo", result.Claims["app_slug"]);
        Assert.Equal(appId.ToString(), result.Claims["app_id"]);
        Assert.True(result.Claims.ContainsKey("role"));
    }

    [Fact]
    public async Task Rejects_Tampered_Token()
    {
        var (provider, issuer) = CreateIssuer();
        using var _ = provider;

        var (token, _) = issuer.IssueAccessToken(
            new TokenSubject(Guid.NewGuid(), "a@b.c", "A", ["member"]),
            Guid.NewGuid(), "demo", "demo-client");

        var parts = token.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}";

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(tampered, new TokenValidationParameters
        {
            ValidIssuer = "https://issuer.test",
            ValidAudience = "demo-client",
            IssuerSigningKey = provider.SecurityKey,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Jwks_Exposes_Rsa_Public_Key()
    {
        var (provider, _) = CreateIssuer();
        using var __ = provider;

        var json = JsonSerializer.Serialize(provider.GetJwksDocument());
        using var document = JsonDocument.Parse(json);
        var key = document.RootElement.GetProperty("keys")[0];

        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.Equal(provider.KeyId, key.GetProperty("kid").GetString());
        Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("n").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("e").GetString()));
    }
}
