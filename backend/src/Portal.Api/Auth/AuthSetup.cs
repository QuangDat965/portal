using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Portal.Domain;
using Portal.Infrastructure.Identity;
using Portal.Infrastructure.Options;
using Portal.Infrastructure.Persistence;

namespace Portal.Api.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddPortalAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // Portal không dùng mật khẩu, chỉ external login.
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PortalDbContext>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        var authentication = services.AddAuthentication(IdentityConstants.ApplicationScheme);

        authentication.AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = "portal.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;

            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        var googleClientId = configuration["Auth:Google:ClientId"];
        if (!string.IsNullOrWhiteSpace(googleClientId))
        {
            authentication.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = configuration["Auth:Google:ClientSecret"] ?? string.Empty;
                options.CallbackPath = "/api/auth/google/callback";
                options.SignInScheme = IdentityConstants.ApplicationScheme;
                options.SaveTokens = false;
                options.Scope.Add("email");
                options.Scope.Add("profile");

                options.Events.OnTicketReceived = async context =>
                {
                    var userManager = context.HttpContext.RequestServices
                        .GetRequiredService<UserManager<AppUser>>();
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("GoogleLogin");

                    var principal = context.Principal;
                    var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                                  ?? principal?.FindFirstValue("sub");
                    var email = principal?.FindFirstValue(ClaimTypes.Email)
                                ?? principal?.FindFirstValue("email");
                    var name = principal?.FindFirstValue(ClaimTypes.Name)
                               ?? principal?.FindFirstValue("name")
                               ?? email;
                    var avatar = principal?.FindFirstValue("picture")
                                 ?? principal?.FindFirstValue("urn:google:picture");

                    if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
                    {
                        context.Fail("Google không trả về email hoặc subject.");
                        return;
                    }

                    var user = await userManager.FindByLoginAsync("Google", subject);
                    if (user is null)
                    {
                        user = await userManager.FindByEmailAsync(email);

                        if (user is null)
                        {
                            user = new AppUser
                            {
                                Id = Guid.NewGuid(),
                                UserName = email,
                                Email = email,
                                EmailConfirmed = true,
                                DisplayName = name ?? email,
                                AvatarUrl = avatar
                            };

                            var created = await userManager.CreateAsync(user);
                            if (!created.Succeeded)
                            {
                                logger.LogError(
                                    "Tạo user từ Google thất bại: {Errors}",
                                    string.Join("; ", created.Errors.Select(e => e.Description)));
                                context.Fail("Không tạo được tài khoản.");
                                return;
                            }

                            await userManager.AddToRoleAsync(user, RoleNames.Member);
                        }

                        await userManager.AddLoginAsync(
                            user, new UserLoginInfo("Google", subject, "Google"));
                    }

                    user.DisplayName = string.IsNullOrWhiteSpace(name) ? user.DisplayName : name;
                    user.AvatarUrl = avatar ?? user.AvatarUrl;
                    user.LastLoginAt = DateTimeOffset.UtcNow;
                    await userManager.UpdateAsync(user);

                    var roles = await userManager.GetRolesAsync(user);
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new(ClaimTypes.Name, user.DisplayName),
                        new(ClaimTypes.Email, user.Email ?? email),
                        new("avatar_url", user.AvatarUrl ?? string.Empty)
                    };
                    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity(
                        claims,
                        IdentityConstants.ApplicationScheme,
                        ClaimTypes.Name,
                        ClaimTypes.Role));
                };
            });
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole(RoleNames.Admin));
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "portal.csrf";
            options.Cookie.HttpOnly = false;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        services.Configure<AuthOptions>(configuration.GetSection("Auth"));

        return services;
    }
}
