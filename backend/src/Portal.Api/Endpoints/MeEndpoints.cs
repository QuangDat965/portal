using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Portal.Api.Auth;
using Portal.Application.Abstractions;
using Portal.Application.Dtos;

namespace Portal.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new MeDto(
                user.GetUserId() ?? Guid.Empty,
                user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                user.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                user.FindFirstValue("avatar_url"),
                user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList())))
            .RequireAuthorization()
            .WithTags("Me");

        app.MapGet("/api/me/apps", async (
                ClaimsPrincipal user,
                IEntitlementService entitlements,
                CancellationToken cancellationToken) =>
            {
                var userId = user.GetUserId();
                if (userId is null)
                {
                    return Results.Unauthorized();
                }

                var apps = await entitlements.GetAppsForUserAsync(
                    userId.Value, user.IsAdmin(), cancellationToken);
                return Results.Ok(apps);
            })
            .RequireAuthorization()
            .WithTags("Me");

        app.MapPost("/api/apps/{slug}/launch", async (
                string slug,
                HttpContext context,
                ClaimsPrincipal user,
                ILaunchService launches,
                CancellationToken cancellationToken) =>
            {
                if (await context.ValidateAntiforgeryAsync() is { } error)
                {
                    return error;
                }

                var userId = user.GetUserId();
                if (userId is null)
                {
                    return Results.Unauthorized();
                }

                var result = await launches.CreateLaunchAsync(
                    userId.Value,
                    slug,
                    user.IsAdmin(),
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Request.Headers.UserAgent.ToString(),
                    cancellationToken);

                return result is null
                    ? Results.NotFound(new { error = "App không tồn tại hoặc bạn không có quyền truy cập." })
                    : Results.Ok(result);
            })
            .RequireAuthorization()
            .RequireRateLimiting("launch")
            .WithTags("Me");

        return app;
    }
}
