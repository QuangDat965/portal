using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Portal.Application.Abstractions;
using Portal.Infrastructure.Options;

namespace Portal.Infrastructure.Security;

public sealed class SecretHasher : ISecretHasher
{
    private readonly byte[] _pepper;

    public SecretHasher(IOptions<SecurityOptions> options)
    {
        _pepper = Encoding.UTF8.GetBytes(options.Value.SecretPepper);
    }

    public string Hash(string value)
    {
        using var hmac = new HMACSHA256(_pepper);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }

    public bool Verify(string hash, string value)
    {
        var computed = Hash(value);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(hash));
    }
}
