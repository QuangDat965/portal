namespace ExplodingKittens.Api.Game;

public sealed class PlayerState
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SeatIndex { get; set; }
    public bool IsHost { get; set; }
    public bool IsEliminated { get; set; }
}

/// <summary>Trạng thái ván chơi ở phía server (ẩn thông tin không cần thiết).</summary>
public sealed class GameState
{
    public Guid RoomId { get; set; }
    public long Revision { get; set; }
    public List<PublicGameEvent> RecentEvents { get; set; } = new();

    /// <summary>Thứ tự lượt chơi cố định theo seat.</summary>
    public List<Guid> TurnOrder { get; set; } = new();

    public List<string> Deck { get; set; } = new();
    public List<string> DiscardPile { get; set; } = new();
    public Dictionary<Guid, List<string>> PlayerHands { get; set; } = new();
    public int CurrentTurnIndex { get; set; }
    public int PendingDraws { get; set; } = 1;
    public Guid? WinnerUserId { get; set; }
    public GameStatus Status { get; set; } = GameStatus.NotStarted;
    public DateTimeOffset? LastActionAt { get; set; }

    /// <summary>Các user đã bị loại (nổ).</summary>
    public HashSet<Guid> EliminatedUserIds { get; set; } = new();

    /// <summary>Cờ kết quả hành động vừa thực hiện (cho log/UI).</summary>
    public bool Exploded { get; set; }
    public bool Defused { get; set; }
    public Guid? EliminatedUserId { get; set; }

    public bool IsEliminated(Guid userId)
        => EliminatedUserIds.Contains(userId);
}

public enum GameStatus
{
    NotStarted = 0,
    Playing = 1,
    Finished = 2
}

public enum RoomStatus
{
    Waiting = 0,
    Playing = 1,
    Finished = 2
}

/// <summary>Lỗi nghiệp vụ game, trả về 400 cho client.</summary>
public sealed class GameError : Exception
{
    public GameError(string message) : base(message) { }
}

/// <summary>Kết quả của một hành động chơi.</summary>
public sealed class PlayResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public GameState? State { get; set; }
    public List<string>? FutureCards { get; set; }
}

/// <summary>Chế độ xem public cho một người chơi (chỉ thấy tay bài của mình).</summary>
public sealed class GameView
{
    public Guid RoomId { get; set; }
    public long Revision { get; set; }
    public List<PublicGameEvent> RecentEvents { get; set; } = new();
    public string? DiscardTop { get; set; }
    public int DiscardCount { get; set; }
    public RoomStatus Status { get; set; }
    public string InviteCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<PlayerView> Players { get; set; } = new();
    public bool IsStarted { get; set; }
    public List<string> OwnHand { get; set; } = new();
    public Dictionary<Guid, int> HandCountByPlayer { get; set; } = new();
    public Guid CurrentTurnUserId { get; set; }
    public int PendingDraws { get; set; }
    public int DeckRemaining { get; set; }
    public Guid? WinnerUserId { get; set; }
}

public sealed class PlayerView
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SeatIndex { get; set; }
    public bool IsHost { get; set; }
    public bool IsEliminated { get; set; }
}

public sealed record PublicGameEvent(
    long Sequence, Guid ActorUserId, string Kind, string? CardType, Guid? TargetUserId);
