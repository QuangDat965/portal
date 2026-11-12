using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using PortalSso;
namespace ExplodingKittens.Api;

public static class AuthEndpoints
{

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/api/info", (IConfiguration configuration) => Results.Ok(new
        {
            app = "exploding-kittens",
            name = configuration["App:Name"] ?? "Mèo Nổ Online",
            description = configuration["App:Description"],
            portalUrl = configuration["Portal:FrontendUrl"] ?? "http://localhost:5173"
        }));

        // SSO callback: đổi launch code lấy JWT, rồi phát cookie session riêng của app.
        app.MapGet("/auth/callback", async (
            string? code,
            HttpContext context,
            PortalSsoClient sso,
            ILoggerFactory loggerFactory,
            IConfiguration configuration) =>
        {
            var logger = loggerFactory.CreateLogger("ExplodingKittens.Sso");
            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.Redirect("/?error=missing_code");
            }

            PortalIdentity identity;
            try
            {
                identity = await sso.RedeemLaunchCodeAsync(code, context.RequestAborted);
            }
            catch (PortalSsoException ex)
            {
                logger.LogWarning("SSO callback thất bại: {Reason}", ex.Message);
                return Results.Redirect("/?error=sso");
            }

            var claims = new List<Claim>
            {
                new("sub", identity.Subject),
                new("email", identity.Email),
                new("name", identity.Name)
            };
            claims.AddRange(identity.Roles.Select(role => new Claim("role", role)));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme));

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(configuration.GetValue("Cookie:SessionHours", 8))
                });

            return Results.Redirect("/");
        }).AllowAnonymous();

        app.MapPost("/auth/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithMetadata(new RequireAntiforgeryTokenAttribute());

        app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
                Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }))
            .RequireAuthorization();

        app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            id = user.FindFirst("sub")?.Value ?? string.Empty,
            email = user.FindFirst("email")?.Value ?? string.Empty,
            name = user.FindFirst("name")?.Value ?? string.Empty,
            roles = user.FindAll("role").Select(c => c.Value).ToArray()
        })).RequireAuthorization();

        return app;
    }
}
