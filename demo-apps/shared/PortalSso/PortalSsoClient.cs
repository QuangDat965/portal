using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PortalSso;

/// <summary>
/// Trao đổi launch code lấy JWT và xác minh JWT bằng JWKS của Portal.
/// Không đọc database của Portal; chỉ dùng hai endpoint công khai:
/// <c>POST /api/auth/token</c> và <c>GET /api/.well-known/jwks.json</c>.
/// </summary>
public sealed class PortalSsoClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly PortalSsoOptions _options;
    private readonly bool _ownsHttpClient;
    private readonly SemaphoreSlim _jwksGate = new(1, 1);

    private IReadOnlyList<SecurityKey> _cachedKeys = [];
    private DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastRefreshAt = DateTimeOffset.MinValue;

    public PortalSsoClient(PortalSsoOptions options)
        : this(options, new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, ownsHttpClient: true)
    {
    }

    public PortalSsoClient(PortalSsoOptions options, HttpClient httpClient, bool ownsHttpClient = false)
    {
        _options = options;
        _http = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _options.Url = _options.Url.TrimEnd('/');
    }

    public async Task<PortalIdentity> RedeemLaunchCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new PortalSsoException("Thiếu launch code.");
        }

        using var response = await _http.PostAsJsonAsync(
            $"{_options.Url}/api/auth/token",
            new TokenRequest(_options.ClientId, _options.ClientSecret, code),
            JsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new PortalSsoException(
                $"Portal từ chối launch code (HTTP {(int)response.StatusCode}).");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenPayload>(
            JsonOptions, cancellationToken);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new PortalSsoException("Portal trả về token rỗng.");
        }

        return await ValidateTokenAsync(payload.AccessToken, cancellationToken);
    }

    private async Task<PortalIdentity> ValidateTokenAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        JsonWebToken jwt;
        try
        {
            jwt = new JsonWebToken(accessToken);
        }
        catch (Exception ex)
        {
            throw new PortalSsoException("Access token không phải JWT hợp lệ.", ex);
        }

        var signingKey = await ResolveSigningKeyAsync(jwt.Kid, cancellationToken);

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(accessToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.ClientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(60),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            NameClaimType = "name",
            RoleClaimType = "role"
        });

        if (!result.IsValid || result.ClaimsIdentity is null)
        {
            throw new PortalSsoException("JWT không hợp lệ hoặc đã hết hạn.");
        }

        var identity = result.ClaimsIdentity;
        var subject = identity.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new PortalSsoException("JWT thiếu claim sub.");
        }

        var roles = identity.FindAll("role")
            .Select(c => c.Value)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var expiresAt = result.SecurityToken is JsonWebToken validated
            ? new DateTimeOffset(validated.ValidTo, TimeSpan.Zero)
            : DateTimeOffset.UtcNow;

        return new PortalIdentity(
            subject,
            identity.FindFirst("email")?.Value ?? string.Empty,
            identity.FindFirst("name")?.Value ?? string.Empty,
            roles,
            expiresAt);
    }

    private async Task<SecurityKey> ResolveSigningKeyAsync(
        string? keyId,
        CancellationToken cancellationToken)
    {
        var keys = await GetKeysAsync(keyId, cancellationToken);
        var key = FindKey(keys, keyId);
        if (key is not null)
        {
            return key;
        }

        // Khoá mới được xoay vòng: tải lại JWKS một lần.
        keys = await GetKeysAsync(keyId, cancellationToken, forceRefresh: true);
        key = FindKey(keys, keyId);

        return key ?? throw new PortalSsoException(
            "Không tìm thấy khoá ký phù hợp trong JWKS của Portal.");
    }

    private static SecurityKey? FindKey(IReadOnlyList<SecurityKey> keys, string? keyId) =>
        keyId is null
            ? keys.FirstOrDefault()
            : keys.FirstOrDefault(k => string.Equals(k.KeyId, keyId, StringComparison.Ordinal));

    private async Task<IReadOnlyList<SecurityKey>> GetKeysAsync(
        string? keyId,
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        await _jwksGate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var cacheValid = _cachedKeys.Count > 0 && now < _cacheExpiresAt;
            var hasKey = keyId is null || FindKey(_cachedKeys, keyId) is not null;

            if (!forceRefresh && cacheValid && hasKey)
            {
                return _cachedKeys;
            }

            if (now - _lastRefreshAt < MinimumRefreshInterval && _cachedKeys.Count > 0)
            {
                return _cachedKeys;
            }

            _lastRefreshAt = now;

            using var response = await _http.GetAsync(
                $"{_options.Url}/api/.well-known/jwks.json", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new PortalSsoException(
                    $"Không tải được JWKS của Portal (HTTP {(int)response.StatusCode}).");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var keys = ParseJwks(json);

            if (keys.Count == 0)
            {
                throw new PortalSsoException("JWKS của Portal không có khoá RSA nào.");
            }

            _cachedKeys = keys;
            _cacheExpiresAt = now.Add(_options.JwksCacheDuration);

            return keys;
        }
        finally
        {
            _jwksGate.Release();
        }
    }

    private static IReadOnlyList<SecurityKey> ParseJwks(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("keys", out var keys)
            || keys.ValueKind != JsonValueKind.Array)
        {
            throw new PortalSsoException("JWKS của Portal không đúng định dạng.");
        }

        var result = new List<SecurityKey>();

        foreach (var item in keys.EnumerateArray())
        {
            if (item.TryGetProperty("kty", out var kty) && kty.GetString() != "RSA")
            {
                continue;
            }

            if (!item.TryGetProperty("n", out var modulus)
                || !item.TryGetProperty("e", out var exponent))
            {
                continue;
            }

            var n = modulus.GetString();
            var e = exponent.GetString();
            if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(e))
            {
                continue;
            }

            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlEncoder.DecodeBytes(n),
                Exponent = Base64UrlEncoder.DecodeBytes(e)
            });

            var keyId = item.TryGetProperty("kid", out var kid) ? kid.GetString() : null;
            result.Add(new RsaSecurityKey(rsa) { KeyId = keyId });
        }

        return result;
    }

    public void Dispose()
    {
        _jwksGate.Dispose();
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }

    private sealed record TokenRequest(string ClientId, string ClientSecret, string Code);

    private sealed record TokenPayload(string AccessToken);
}
