using Microsoft.AspNetCore.Mvc;
using Portal.Api.Contracts;
using Portal.Application.Abstractions;

namespace Portal.Api.Endpoints;

public static class TokenEndpoints
{
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/token", async (
                [FromBody] ExchangeTokenRequest request,
                ILaunchService launches,
                CancellationToken cancellationToken) =>
            {
                var result = await launches.ExchangeAsync(
                    request.ClientId,
                    request.ClientSecret,
                    request.Code,
                    cancellationToken);

                if (result is null)
                {
                    return Results.BadRequest(new { error = "invalid_grant" });
                }

                return Results.Ok(result);
            })
            .AllowAnonymous()
            .RequireRateLimiting("token")
            .WithTags("Auth");

        app.MapGet("/api/.well-known/jwks.json", (ISigningKeyProvider keys) =>
                Results.Ok(keys.GetJwksDocument()))
            .AllowAnonymous()
            .WithTags("Auth");

        return app;
    }
}
