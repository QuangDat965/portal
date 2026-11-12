using ExplodingKittens.Api;
using ExplodingKittens.Api.Game;
using ExplodingKittens.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExplodingKittens.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EK_TEST_POSTGRES")))
            Skip = "Set EK_TEST_POSTGRES to a disposable PostgreSQL database connection string.";
    }
}

public sealed class PersistenceTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("EK_TEST_POSTGRES"),
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "exploding_kittens")).Options);
    private static GameEngineService Service(AppDbContext db) => new(db, NullLogger<GameEngineService>.Instance);

    [PostgresFact]
    public async Task GamePersistsRoomIdHandsEliminationAndWinnerAtomically()
    {
        var host = Guid.NewGuid(); var guest = Guid.NewGuid();
        await using var db = Db();
        await db.Database.MigrateAsync();
        var game = Service(db);
        var room = await game.CreateRoomAsync("Persistence", host, "Host", default);
        await game.JoinRoomAsync(room.Id, guest, "Guest", default);
        await game.StartGameAsync(room.Id, host, default);
        var hostView = await game.GetPublicViewAsync(room.Id, host, default);
        Assert.Equal(5, hostView.OwnHand.Count);
        Assert.Equal(2, hostView.HandCountByPlayer.Count);
        Assert.Equal(room.Id, (await game.LoadStateAsync(room.Id, default)).RoomId);
        await Assert.ThrowsAsync<GameError>(() => game.GetPublicViewAsync(room.Id, Guid.NewGuid(), default));

        // Force the next draw to explode without a defuse, using persisted server state.
        var entity = await db.GameStates.SingleAsync(g => g.RoomId == room.Id);
        entity.PlayerHandsJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<Guid, string[]>
            { [host] = [], [guest] = [CardTypes.Skip] });
        entity.DeckJson = "[\"exploding_kitten\"]";
        await db.SaveChangesAsync();
        await game.DrawCardAsync(room.Id, host, default);

        await using var fresh = Db();
        var final = await Service(fresh).GetPublicViewAsync(room.Id, guest, default);
        Assert.Equal(RoomStatus.Finished, final.Status);
        Assert.Equal(guest, final.WinnerUserId);
        Assert.True(final.Players.Single(p => p.UserId == host).IsEliminated);
        Assert.Equal(new[] { CardTypes.Skip }, final.OwnHand);
        await Assert.ThrowsAsync<GameError>(() => Service(fresh).DrawCardAsync(room.Id, host, default));
    }

    [PostgresFact]
    public async Task ConcurrentJoinsCannotExceedFiveSeatsAndExistingPlayerCanReconnect()
    {
        Guid roomId; var host = Guid.NewGuid();
        await using (var db = Db())
        {
            await db.Database.MigrateAsync();
            roomId = (await Service(db).CreateRoomAsync("Concurrent", host, "Host", default)).Id;
        }
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            await using var db = Db();
            try { await Service(db).JoinRoomAsync(roomId, Guid.NewGuid(), $"Guest {i}", default); return true; }
            catch (GameError error) { Assert.Equal("room_full", error.Message); return false; }
        }));
        Assert.Equal(4, outcomes.Count(ok => ok));
        await using var check = Db();
        var service = Service(check);
        var view = await service.GetPublicViewAsync(roomId, host, default);
        Assert.Equal(5, view.Players.Count);
        Assert.Equal(5, view.Players.Select(p => p.SeatIndex).Distinct().Count());
        await service.StartGameAsync(roomId, host, default);
        await service.JoinRoomAsync(roomId, host, "Host", default);
        Assert.Equal(5, (await service.GetPublicViewAsync(roomId, host, default)).Players.Count);
    }

    [PostgresFact]
    public async Task PublicEventsAreOrderedPersistedAndNeverRevealPrivateDrawsOrFuture()
    {
        await using var db = Db();
        await db.Database.MigrateAsync();
        var game = Service(db);
        var host = Guid.NewGuid(); var guest = Guid.NewGuid();
        var room = await game.CreateRoomAsync("Events", host, "Host", default);
        await game.JoinRoomAsync(room.Id, guest, "Guest", default);
        await game.StartGameAsync(room.Id, host, default);
        var entity = await db.GameStates.SingleAsync(g => g.RoomId == room.Id);
        entity.PlayerHandsJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<Guid, string[]>
            { [host] = [CardTypes.SeeTheFuture], [guest] = [CardTypes.Defuse] });
        entity.DeckJson = "[\"exploding_kitten\",\"shuffle\",\"attack\"]";
        await db.SaveChangesAsync();
        var privateResult = await game.PlayCardAsync(room.Id, host, CardTypes.SeeTheFuture, null, default);
        Assert.Equal(new[] { CardTypes.Attack, CardTypes.Shuffle, CardTypes.ExplodingKitten }, privateResult.FutureCards);
        await game.DrawCardAsync(room.Id, host, default);
        await using var fresh = Db();
        var publicView = await Service(fresh).GetPublicViewAsync(room.Id, guest, default);
        Assert.Equal(3, publicView.Revision);
        Assert.Equal(new long[] { 1, 2, 3 }, publicView.RecentEvents.Select(e => e.Sequence));
        Assert.Equal(CardTypes.SeeTheFuture, publicView.DiscardTop);
        Assert.Equal(1, publicView.DiscardCount);
        Assert.Equal("drawn", publicView.RecentEvents[^1].Kind);
        Assert.Null(publicView.RecentEvents[^1].CardType);
        Assert.Equal(new[] { CardTypes.Defuse }, publicView.OwnHand);
        var json = System.Text.Json.JsonSerializer.Serialize(publicView);
        Assert.DoesNotContain(CardTypes.Attack, json);
        Assert.DoesNotContain(CardTypes.Shuffle, json);
        Assert.DoesNotContain(CardTypes.ExplodingKitten, json);
    }

    [PostgresFact]
    public async Task ConcurrentDrawsDoNotOverwriteOrTakeAnExtraTurn()
    {
        Guid roomId; var host = Guid.NewGuid(); var guest = Guid.NewGuid();
        await using (var db = Db())
        {
            await db.Database.MigrateAsync();
            var service = Service(db);
            roomId = (await service.CreateRoomAsync("Draw", host, "Host", default)).Id;
            await service.JoinRoomAsync(roomId, guest, "Guest", default);
            await service.StartGameAsync(roomId, host, default);
            var entity = await db.GameStates.SingleAsync(g => g.RoomId == roomId);
            entity.DeckJson = "[\"skip\",\"shuffle\",\"attack\"]";
            await db.SaveChangesAsync();
        }
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = Db();
            try { await Service(db).DrawCardAsync(roomId, host, default); return true; }
            catch (GameError error) { Assert.Equal("not_turn", error.Message); return false; }
        }));
        Assert.Single(outcomes, ok => ok);
        await using var check = Db();
        var final = await Service(check).GetPublicViewAsync(roomId, host, default);
        Assert.Equal(6, final.OwnHand.Count);
        Assert.Equal(2, final.DeckRemaining);
        Assert.Equal(guest, final.CurrentTurnUserId);
        Assert.Equal(2, final.Revision);
        Assert.Equal(new long[] { 1, 2 }, final.RecentEvents.Select(e => e.Sequence));
    }
}
