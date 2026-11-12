using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ExplodingKittens.Api.Game;

namespace ExplodingKittens.Api;

/// <summary>
/// Hub realtime cho phòng chơi. Mọi kết nối phải xác thực bằng cookie session của app.
/// Client chỉ nhận state qua server; server không bao giờ gửi tay bài của người khác.
/// </summary>
[Authorize]
public sealed class GameHub : Hub
{
    private readonly GameEngineService _gameService;

    public GameHub(GameEngineService gameService)
    {
        _gameService = gameService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));
        await base.OnConnectedAsync();
    }

    public async Task JoinRoom(Guid roomId)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            Context.Abort();
            return;
        }

        var room = await _gameService.GetRoomAsync(roomId, Context.ConnectionAborted);
        if (room is null || room.Players.All(p => p.UserId != userId))
        {
            throw new HubException("Bạn không trong phòng này.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
        await BroadcastState(roomId);
    }

    public async Task LeaveRoom(Guid roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    public async Task RequestState(Guid roomId)
        => await BroadcastState(roomId);

    private async Task BroadcastState(Guid roomId)
    {
        var userId = GetUserId();
        if (userId is null) throw new HubException("unauthorized");
        try
        {
            var view = await _gameService.GetPublicViewAsync(roomId, userId.Value, Context.ConnectionAborted);
            await Clients.Caller.SendAsync("State", view, Context.ConnectionAborted);
        }
        catch (GameError) { throw new HubException("not_player"); }
    }

    private Guid? GetUserId()
    {
        var raw = Context.User?.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static string UserGroup(Guid userId) => $"user:{userId}";
    public static string RoomGroup(Guid roomId) => $"room:{roomId}";
}
