using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Portal.Infrastructure.Identity;

namespace Portal.Api.Auth;

/// <summary>
/// Cookie principal dùng DisplayName/avatar thay vì username (email),
/// để UI và các app hiển thị đúng tên người dùng.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<AppUser, IdentityRole<Guid>>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        foreach (var claim in identity.FindAll(ClaimTypes.Name).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(
            ClaimTypes.Name,
            string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.UserName ?? user.Email ?? string.Empty
                : user.DisplayName));

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            identity.AddClaim(new Claim("avatar_url", user.AvatarUrl));
        }

        return identity;
    }
}
