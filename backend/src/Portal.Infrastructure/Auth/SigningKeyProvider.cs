using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Portal.Application.Abstractions;

namespace Portal.Infrastructure.Auth;

/// <summary>
/// Load khoá RSA ký JWT từ cấu hình/file. Nếu chưa có, tự sinh khoá dev và ghi ra file.
/// Production nên truyền Jwt:PrivateKeyPem qua secret manager.
/// </summary>
public sealed class SigningKeyProvider : ISigningKeyProvider, IDisposable
{
    private readonly RSA _rsa;

    public SigningKeyProvider(IConfiguration configuration, ILogger<SigningKeyProvider> logger)
    {
        var pem = configuration["Jwt:PrivateKeyPem"];
        var path = configuration["Jwt:PrivateKeyPath"];

        if (!string.IsNullOrWhiteSpace(pem))
        {
            _rsa = RSA.Create();
            _rsa.ImportFromPem(pem);
        }
        else
        {
            path = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(AppContext.BaseDirectory, "keys", "jwt-private.pem")
                : path;

            if (File.Exists(path))
            {
                _rsa = RSA.Create();
                _rsa.ImportFromPem(File.ReadAllText(path));
            }
            else
            {
                logger.LogWarning(
                    "Chưa có JWT private key, đang sinh khoá mới tại {Path}. " +
                    "Không dùng khoá tự sinh cho production.", path);

                _rsa = RSA.Create(2048);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, _rsa.ExportPkcs8PrivateKeyPem());
            }
        }

        SecurityKey = new RsaSecurityKey(_rsa)
        {
            KeyId = ComputeKeyId(_rsa)
        };
    }

    public RsaSecurityKey SecurityKey { get; }

    public string KeyId => SecurityKey.KeyId;

    public object GetJwksDocument()
    {
        var parameters = _rsa.ExportParameters(false);

        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = KeyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus),
                    e = Base64UrlEncoder.Encode(parameters.Exponent)
                }
            }
        };
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var hash = SHA256.HashData(rsa.ExportSubjectPublicKeyInfo());
        return Base64UrlEncoder.Encode(hash)[..16];
    }

    public void Dispose() => _rsa.Dispose();
}
