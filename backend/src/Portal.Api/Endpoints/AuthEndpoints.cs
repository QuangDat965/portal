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
