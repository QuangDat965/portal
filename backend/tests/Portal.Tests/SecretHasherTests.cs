using Microsoft.Extensions.Options;
using Portal.Infrastructure.Options;
using Portal.Infrastructure.Security;

namespace Portal.Tests;

public class SecretHasherTests
{
    private static SecretHasher CreateHasher() => new(
        Options.Create(new SecurityOptions { SecretPepper = "pepper-for-tests" }));

    [Fact]
    public void Hash_Is_Stable_And_Does_Not_Contain_Plaintext()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash("super-secret");

        Assert.Equal(hash, hasher.Hash("super-secret"));
        Assert.DoesNotContain("super-secret", hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void Verify_Accepts_Correct_Value_And_Rejects_Others()
    {
        var hasher = CreateHasher();
        var hash = hasher.Hash("super-secret");

        Assert.True(hasher.Verify(hash, "super-secret"));
        Assert.False(hasher.Verify(hash, "super-secret-2"));
        Assert.False(hasher.Verify("", "super-secret"));
    }
}
