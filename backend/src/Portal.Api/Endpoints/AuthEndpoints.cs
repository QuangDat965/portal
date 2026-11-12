using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Portal.Api.Auth;
using Portal.Api.Contracts;
using Portal.Domain;
using Portal.Infrastructure.Identity;
using Portal.Infrastructure.Options;

namespace Portal.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapGet("/google", (IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(configuration["Auth:Google:ClientId"]))
            {
                return Results.Problem(
                    title: "Google OAuth chưa được cấu hình.",
                    detail: "Điền Auth:Google:ClientId và ClientSecret, hoặc dùng dev login.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var properties = new AuthenticationProperties
            {
                RedirectUri = "/api/auth/google/complete"
            };

            return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
        });

        group.MapGet("/google/complete", async (
            HttpContext context,
            IOptions<AuthOptions> authOptions) =>
        {
            var frontend = authOptions.Value.FrontendUrl.TrimEnd('/');
            var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);

            return result.Succeeded
                ? Results.Redirect($"{frontend}/apps")
                : Results.Redirect($"{frontend}/login?error=google");
        });

        group.MapPost("/password-login", async (
            PasswordLoginRequest request,
            HttpContext context,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ILoggerFactory loggerFactory) =>
        {
            if (await context.ValidateAntiforgeryAsync() is { } csrfError)
            {
                return csrfError;
            }

            var identifier = request.Email?.Trim();
            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrEmpty(request.Password))
            {
                return Results.BadRequest(new
                {
                    error = "Email/tên đăng nhập và mật khẩu là bắt buộc."
                });
            }

            var user = await userManager.FindByEmailAsync(identifier)
                       ?? await userManager.FindByNameAsync(identifier);

            if (user is null || !await userManager.HasPasswordAsync(user))
            {
                return Results.Json(
                    new { error = "Email/tên đăng nhập hoặc mật khẩu không đúng." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await signInManager.PasswordSignInAsync(
                user, request.Password, isPersistent: true, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                loggerFactory.CreateLogger("PasswordLogin").LogWarning(
                    "Tài khoản {Email} bị tạm khoá do đăng nhập sai nhiều lần.", user.Email);

                return Results.Json(
                    new { error = "Tài khoản tạm khoá do đăng nhập sai nhiều lần. Thử lại sau ít phút." },
                    statusCode: StatusCodes.Status423Locked);
            }

            if (!result.Succeeded)
            {
                return Results.Json(
                    new { error = "Email/tên đăng nhập hoặc mật khẩu không đúng." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            user.LastLoginAt = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);

            loggerFactory.CreateLogger("PasswordLogin").LogInformation(
                "Password login cho {Email}.", user.Email);

            return Results.Ok(new { ok = true });
        }).RequireRateLimiting("login");

        group.MapPost("/dev-login", async (
            DevLoginRequest request,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IOptions<AuthOptions> authOptions,
            ILoggerFactory loggerFactory) =>
        {
            if (!authOptions.Value.AllowDevLogin)
            {
                return Results.Problem(
                    title: "Dev login đang tắt.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var email = request.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                return Results.BadRequest(new { error = "Email không hợp lệ." });
            }

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new AppUser
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = string.IsNullOrWhiteSpace(request.Name)
                        ? email.Split('@')[0]
                        : request.Name.Trim()
                };

                var created = await userManager.CreateAsync(user);
                if (!created.Succeeded)
                {
                    return Results.BadRequest(new
                    {
                        error = string.Join("; ", created.Errors.Select(e => e.Description))
                    });
                }

                await userManager.AddToRoleAsync(user, RoleNames.Member);
            }

            user.LastLoginAt = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);
            await signInManager.SignInAsync(user, isPersistent: true);

            loggerFactory.CreateLogger("DevLogin").LogInformation(
                "Dev login cho {Email}.", email);

            return Results.Ok(new { ok = true });
        });

        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        group.MapPost("/logout", async (HttpContext context) =>
        {
            if (await context.ValidateAntiforgeryAsync() is { } error)
            {
                return error;
            }

            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }
}
