using System.Security.Claims;
using ExplodingKittens.Api.Game;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.SignalR;

namespace ExplodingKittens.Api.Endpoints;

public static class GameEndpoints
{
    public sealed record CreateRoomRequest(string? Name);
    public sealed record JoinRoomRequest(string? InviteCode);
    public sealed record PlayCardRequest(string? CardType, Guid? TargetUserId);

    public static void MapGameEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.AddEndpointFilter(async (context, next) =>
        {
            if (!Guid.TryParse(context.HttpContext.User.FindFirstValue("sub"), out _))
                return Results.Unauthorized();
            try
            {
                if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
                    await context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>()
                        .ValidateRequestAsync(context.HttpContext);
                return await next(context);
            }
            catch (AntiforgeryValidationException) { return Results.BadRequest(new { error = "csrf" }); }
            catch (GameError error) { return Results.BadRequest(new { error = error.Message }); }
        });

        api.MapPost("/rooms", async (CreateRoomRequest request, ClaimsPrincipal user,
            GameEngineService game, CancellationToken ct) =>
        {
            var room = await game.CreateRoomAsync(request.Name ?? "", UserId(user), user.FindFirstValue("name") ?? "", ct);
            return Results.Ok(await game.GetPublicViewAsync(room.Id, UserId(user), ct));
        });
        api.MapPost("/rooms/join", async (JoinRoomRequest request, ClaimsPrincipal user,
            GameEngineService game, IHubContext<GameHub> hub, CancellationToken ct) =>
        {
            var code = request.InviteCode?.Trim();
            if (code is not { Length: 6 }) throw new GameError("invalid_invite");
            var room = await game.GetRoomByInviteCodeAsync(code, ct) ?? throw new GameError("invalid_invite");
            await game.JoinRoomAsync(room.Id, UserId(user), user.FindFirstValue("name") ?? "", ct);
            await Notify(hub, room.Id, ct);
            return Results.Ok(await game.GetPublicViewAsync(room.Id, UserId(user), ct));
        });
        api.MapGet("/rooms/{roomId:guid}/state", async (Guid roomId, ClaimsPrincipal user,
            GameEngineService game, CancellationToken ct) =>
            Results.Ok(await game.GetPublicViewAsync(roomId, UserId(user), ct)));
        api.MapPost("/rooms/{roomId:guid}/start", async (Guid roomId, ClaimsPrincipal user,
            GameEngineService game, IHubContext<GameHub> hub, CancellationToken ct) =>
        {
            await game.StartGameAsync(roomId, UserId(user), ct);
            await Notify(hub, roomId, ct);
            return Results.Ok(await game.GetPublicViewAsync(roomId, UserId(user), ct));
        });
        api.MapPost("/games/{roomId:guid}/draw", async (Guid roomId, ClaimsPrincipal user,
            GameEngineService game, IHubContext<GameHub> hub, CancellationToken ct) =>
        {
            var result = await game.DrawCardAsync(roomId, UserId(user), ct);
            await Notify(hub, roomId, ct);
            return Results.Ok(new
            {
                view = await game.GetPublicViewAsync(roomId, UserId(user), ct),
                exploded = result.State!.Exploded, defused = result.State.Defused
            });
        });
        api.MapPost("/games/{roomId:guid}/play", async (Guid roomId, PlayCardRequest request,
            ClaimsPrincipal user, GameEngineService game, IHubContext<GameHub> hub, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.CardType)) throw new GameError("missing_card");
            var result = await game.PlayCardAsync(roomId, UserId(user), request.CardType, request.TargetUserId, ct);
            await Notify(hub, roomId, ct);
            return Results.Ok(new
            {
                view = await game.GetPublicViewAsync(roomId, UserId(user), ct),
                futureCards = result.FutureCards
            });
        });
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
    private static Task Notify(IHubContext<GameHub> hub, Guid roomId, CancellationToken ct)
        => hub.Clients.Group(GameHub.RoomGroup(roomId)).SendAsync("RoomChanged", roomId, ct);
}
