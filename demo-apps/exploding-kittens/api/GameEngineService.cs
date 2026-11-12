using System.Text.Json;
using ExplodingKittens.Api.Game;
using ExplodingKittens.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExplodingKittens.Api;

/// <summary>
/// Điều phối phòng và ván chơi Mèo Nổ. Toàn bộ quyết định (rút bài, xáo bài,
/// loại người chơi, thứ tự lượt) đều do server đưa ra; client không được tin cậy.
/// </summary>
public sealed class GameEngineService
{
    private const string IdAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int InviteCodeLength = 6;

    private readonly AppDbContext _db;
    private readonly ILogger<GameEngineService> _logger;

    public GameEngineService(AppDbContext db, ILogger<GameEngineService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ---------------------------------------------------------------- rooms

    public async Task<RoomEntity> CreateRoomAsync(
        string name, Guid creatorUserId, string creatorName, CancellationToken ct)
    {
        if (name.Length > 80) throw new GameError("name_length");
        var code = await GenerateUniqueInviteCodeAsync(ct);
        var room = new RoomEntity
        {
            Id = Guid.NewGuid(),
            InviteCode = code,
            Name = string.IsNullOrWhiteSpace(name) ? "Phòng mới" : name.Trim(),
            CreatorUserId = creatorUserId,
            Status = RoomStatus.Waiting,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Rooms.Add(room);

        _db.RoomPlayers.Add(new RoomPlayerEntity
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            UserId = creatorUserId,
            Name = string.IsNullOrWhiteSpace(creatorName) ? "Chủ phòng" : creatorName,
            SeatIndex = 0,
            IsHost = true,
            IsEliminated = false,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Created room {RoomId} by {User}.", room.Id, creatorUserId);
        return room;
    }

    public async Task<RoomEntity?> GetRoomAsync(Guid roomId, CancellationToken ct)
        => await _db.Rooms
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);

    public async Task<RoomEntity?> GetRoomByInviteCodeAsync(string code, CancellationToken ct)
        => await _db.Rooms.AsNoTracking()
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.InviteCode == code.Trim().ToUpperInvariant(), ct);

    public Task<RoomPlayerEntity> JoinRoomAsync(Guid roomId, Guid userId, string name, CancellationToken ct)
        => MutateRoomAsync(roomId, () => JoinRoomCoreAsync(roomId, userId, name, ct), ct);

    public Task StartGameAsync(Guid roomId, Guid userId, CancellationToken ct)
        => MutateRoomAsync(roomId, async () =>
        {
            await StartGameCoreAsync(roomId, userId, ct);
            return true;
        }, ct);

    private async Task<RoomPlayerEntity> JoinRoomCoreAsync(
        Guid roomId, Guid userId, string userName, CancellationToken ct)
    {
        var room = await GetRoomAsync(roomId, ct);
        if (room is null) throw new GameError("room_missing");
        var existing = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (existing is not null) return existing;
        if (room.Status != RoomStatus.Waiting) throw new GameError("already_started");
        if (room.Players.Count >= 5) throw new GameError("room_full");

        var player = new RoomPlayerEntity
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(userName) ? "Người chơi" : userName,
            SeatIndex = room.Players.Count,
            IsHost = room.Players.Count == 0,
            IsEliminated = false,
            JoinedAt = DateTimeOffset.UtcNow,
        };
        _db.RoomPlayers.Add(player);
        await _db.SaveChangesAsync(ct);
        return player;
    }

    private async Task StartGameCoreAsync(Guid roomId, Guid userId, CancellationToken ct)
    {
        var room = await GetRoomAsync(roomId, ct);
        if (room is null) throw new GameError("room_missing");
        if (room.CreatorUserId != userId) throw new GameError("host_only");
        if (room.Status != RoomStatus.Waiting) throw new GameError("already_started");
        if (room.Players.Count < 2) throw new GameError("player_count");

        var players = room.Players
            .OrderBy(p => p.SeatIndex)
            .Select(p => new PlayerState
            {
                UserId = p.UserId,
                Name = p.Name,
                SeatIndex = p.SeatIndex,
                IsHost = p.IsHost,
                IsEliminated = false,
            })
            .ToList();

        var state = GameRules.Create(roomId, players.Select(p => p.UserId).ToList());

        RecordEvent(state, userId, "started", null, null);
        _db.GameStates.RemoveRange(await _db.GameStates.Where(g => g.RoomId == roomId).ToListAsync(ct));
        _db.GameStates.Add(BindState(state));

        room.Status = RoomStatus.Playing;
        room.StartedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Bắt đầu ván phòng {RoomId}.", roomId);
    }

    // ---------------------------------------------------------------- plays

    public Task<PlayResult> DrawCardAsync(Guid roomId, Guid userId, CancellationToken ct)
        => MutateRoomAsync(roomId, async () =>
        {
            var state = await LoadStateAsync(roomId, ct);
            var result = GameRules.Draw(state, userId);
            RecordEvent(state, userId, state.Defused ? "defused" : state.Exploded ? "exploded" : "drawn", null, null);
            await SaveStateAsync(roomId, state, ct);
            return result;
        }, ct);

    public Task<PlayResult> PlayCardAsync(
        Guid roomId, Guid userId, string cardType, Guid? targetUserId, CancellationToken ct)
        => MutateRoomAsync(roomId, async () =>
        {
            var state = await LoadStateAsync(roomId, ct);
            var result = GameRules.Play(state, userId, cardType, targetUserId);
            RecordEvent(state, userId, "played", cardType, cardType == CardTypes.Favor ? targetUserId : null);
            await SaveStateAsync(roomId, state, ct);
            return result;
        }, ct);

    // ---------------------------------------------------------------- read

    public async Task<GameView> GetPublicViewAsync(Guid roomId, Guid userId, CancellationToken ct)
    {
        var room = await GetRoomAsync(roomId, ct);
        if (room is null) throw new GameError("room_missing");
        if (room.Players.All(p => p.UserId != userId)) throw new GameError("not_player");

        var state = await LoadStateAsync(roomId, ct);

        var ownHand = state.PlayerHands.GetValueOrDefault(userId) ?? new List<string>();

        return new GameView
        {
            RoomId = roomId,
            Revision = state.Revision,
            RecentEvents = state.RecentEvents,
            DiscardTop = state.DiscardPile.LastOrDefault(),
            DiscardCount = state.DiscardPile.Count,
            Status = state.Status switch { GameStatus.Playing => RoomStatus.Playing, GameStatus.Finished => RoomStatus.Finished, _ => room.Status },
            InviteCode = room.InviteCode,
            Name = room.Name,
            Players = room.Players.OrderBy(p => p.SeatIndex).Select(p => new PlayerView
            {
                UserId = p.UserId,
                Name = p.Name,
                SeatIndex = p.SeatIndex,
                IsHost = p.IsHost,
                IsEliminated = state.Status != GameStatus.NotStarted && state.IsEliminated(p.UserId),
            }).ToList(),
            IsStarted = state.Status == GameStatus.Playing || state.Status == GameStatus.Finished,
            OwnHand = ownHand,
            HandCountByPlayer = state.TurnOrder.ToDictionary(
                id => id,
                id => state.PlayerHands.GetValueOrDefault(id)?.Count ?? 0),
            CurrentTurnUserId = state.Status == GameStatus.Playing
                ? state.TurnOrder[state.CurrentTurnIndex]
                : Guid.Empty,
            PendingDraws = state.PendingDraws,
            DeckRemaining = state.Deck.Count,
            WinnerUserId = state.WinnerUserId,
        };
    }

    public async Task<GameState> LoadStateAsync(Guid roomId, CancellationToken ct)
    {
        var entity = await _db.GameStates.FirstOrDefaultAsync(g => g.RoomId == roomId, ct);
        if (entity is null)
        {
            var room = await _db.Rooms.Include(r => r.Players).FirstOrDefaultAsync(r => r.Id == roomId, ct);
            if (room is null) throw new GameError("room_missing");

            // Trả trạng thái trống cho phòng chưa bắt đầu
            return new GameState
            {
                RoomId = roomId,
                TurnOrder = room.Players.OrderBy(p => p.SeatIndex).Select(p => p.UserId).ToList(),
                PlayerHands = room.Players.ToDictionary(p => p.UserId, _ => new List<string>()),
                Status = GameStatus.NotStarted,
            };
        }

        return new GameState
        {
            RoomId = entity.RoomId,
            Revision = entity.Revision,
            RecentEvents = Deserialize<List<PublicGameEvent>>(entity.RecentEventsJson) ?? new(),
            TurnOrder = Deserialize<List<Guid>>(entity.TurnOrderJson) ?? new(),
            Deck = Deserialize<List<string>>(entity.DeckJson) ?? new(),
            DiscardPile = Deserialize<List<string>>(entity.DiscardPileJson) ?? new(),
            PlayerHands = Deserialize<Dictionary<string, List<string>>>(entity.PlayerHandsJson)?
                .ToDictionary(k => Guid.Parse(k.Key), v => v.Value) ?? new(),
            CurrentTurnIndex = entity.CurrentTurnIndex,
            PendingDraws = entity.PendingDraws,
            WinnerUserId = entity.WinnerUserId,
            Status = entity.Status,
            LastActionAt = entity.LastActionAt,
            EliminatedUserIds = Deserialize<List<string>>(entity.EliminatedUserIdsJson)
                ?.Select(Guid.Parse).ToHashSet() ?? new(),
        };
    }

    private async Task SaveStateAsync(Guid roomId, GameState state, CancellationToken ct)
    {
        var entity = await _db.GameStates.FirstOrDefaultAsync(g => g.RoomId == roomId, ct);
        if (entity is null)
        {
            entity = new GameStateEntity { RoomId = roomId };
            _db.GameStates.Add(entity);
        }

        entity.Revision = state.Revision;
        entity.RecentEventsJson = Serialize(state.RecentEvents);
        entity.TurnOrderJson = Serialize(state.TurnOrder);
        entity.DeckJson = Serialize(state.Deck);
        entity.DiscardPileJson = Serialize(state.DiscardPile);
        entity.PlayerHandsJson = Serialize(
            state.PlayerHands.ToDictionary(k => k.Key.ToString(), v => v.Value));
        entity.CurrentTurnIndex = state.CurrentTurnIndex;
        entity.PendingDraws = state.PendingDraws;
        entity.WinnerUserId = state.WinnerUserId;
        entity.Status = state.Status;
        entity.LastActionAt = DateTimeOffset.UtcNow;
        entity.EliminatedUserIdsJson = Serialize(state.EliminatedUserIds.Select(id => id.ToString()).ToList());

        if (state.Status == GameStatus.Finished)
        {
            var room = await _db.Rooms.SingleAsync(r => r.Id == roomId, ct);
            room.Status = RoomStatus.Finished;
            room.FinishedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    private static GameStateEntity BindState(GameState state)
        => new()
        {
            RoomId = state.RoomId,
            Revision = state.Revision,
            RecentEventsJson = Serialize(state.RecentEvents),
            TurnOrderJson = Serialize(state.TurnOrder),
            DeckJson = Serialize(state.Deck),
            DiscardPileJson = Serialize(state.DiscardPile),
            PlayerHandsJson = Serialize(state.PlayerHands.ToDictionary(k => k.Key.ToString(), v => v.Value)),
            CurrentTurnIndex = state.CurrentTurnIndex,
            PendingDraws = state.PendingDraws,
            WinnerUserId = state.WinnerUserId,
            Status = state.Status,
            LastActionAt = DateTimeOffset.UtcNow,
            EliminatedUserIdsJson = Serialize(new List<string>()),
        };

    private static void RecordEvent(GameState state, Guid userId, string kind, string? cardType, Guid? target)
    {
        state.Revision++;
        state.RecentEvents.Add(new PublicGameEvent(state.Revision, userId, kind, cardType, target));
        if (state.RecentEvents.Count > 32) state.RecentEvents.RemoveRange(0, state.RecentEvents.Count - 32);
    }

    // PostgreSQL transaction locks serialize room mutations across all API instances.
    private async Task<T> MutateRoomAsync<T>(Guid roomId, Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({roomId.ToString()}, 0))", ct);
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    }

    private async Task<string> GenerateUniqueInviteCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = RandomString(IdAlphabet, InviteCodeLength);
            var exists = await _db.Rooms.AnyAsync(r => r.InviteCode == code, ct);
            if (!exists) return code;
        }
        throw new GameError("invite_failed");
    }

    private static string RandomString(string alphabet, int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }
        return new string(chars);
    }

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value);

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return default; }
    }
}
