namespace ExplodingKittens.Api.Persistence;

using Microsoft.EntityFrameworkCore;
using ExplodingKittens.Api.Game;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RoomEntity> Rooms { get; set; }
    public DbSet<RoomPlayerEntity> RoomPlayers { get; set; }
    public DbSet<GameStateEntity> GameStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("exploding_kittens");
        modelBuilder.Entity<RoomEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.InviteCode).IsUnique();
            e.Property(x => x.Status).HasConversion<string>();
        });

        modelBuilder.Entity<RoomPlayerEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RoomId, x.UserId }).IsUnique();
            e.Property(x => x.IsHost).HasDefaultValue(false);
            e.Property(x => x.IsEliminated).HasDefaultValue(false);
        });

        modelBuilder.Entity<GameStateEntity>(e =>
        {
            e.HasKey(x => x.RoomId);
            e.HasOne(x => x.Room).WithOne(x => x.GameState).HasForeignKey<GameStateEntity>(x => x.RoomId);
            e.Property(x => x.Status).HasConversion<string>();
        });
    }
}

public sealed class RoomEntity
{
    public Guid Id { get; set; }
    public string InviteCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public Guid? CreatorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public List<RoomPlayerEntity> Players { get; set; } = new();
    public GameStateEntity? GameState { get; set; }
}

public sealed class RoomPlayerEntity
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SeatIndex { get; set; }
    public bool IsHost { get; set; }
    public bool IsEliminated { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public RoomEntity Room { get; set; } = null!;
}

public sealed class GameStateEntity
{
    public Guid RoomId { get; set; }
    public long Revision { get; set; }
    public string RecentEventsJson { get; set; } = "[]";
    public string TurnOrderJson { get; set; } = "[]";
    public string DeckJson { get; set; } = "[]";
    public string DiscardPileJson { get; set; } = "[]";
    public string PlayerHandsJson { get; set; } = "{}";
    public string EliminatedUserIdsJson { get; set; } = "[]";
    public int CurrentTurnIndex { get; set; } = 0;
    public int PendingDraws { get; set; } = 1;
    public Guid? WinnerUserId { get; set; }
    public GameStatus Status { get; set; } = GameStatus.NotStarted;
    public DateTimeOffset? LastActionAt { get; set; }

    public RoomEntity Room { get; set; } = null!;
}
