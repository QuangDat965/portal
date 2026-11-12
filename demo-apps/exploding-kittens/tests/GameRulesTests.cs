using ExplodingKittens.Api.Game;

namespace ExplodingKittens.Tests;

public sealed class GameRulesTests
{
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void DealHasSafeHandsAndConservesCards(int count)
    {
        for (var iteration = 0; iteration < 30; iteration++)
        {
            var state = GameRules.Create(Guid.NewGuid(), Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList());
            Assert.NotEqual(Guid.Empty, state.RoomId);
            Assert.Equal(count - 1, state.Deck.Count(c => c == CardTypes.ExplodingKitten));
            Assert.All(state.PlayerHands.Values, hand =>
            {
                Assert.Equal(5, hand.Count);
                Assert.Contains(CardTypes.Defuse, hand);
                Assert.DoesNotContain(CardTypes.ExplodingKitten, hand);
                Assert.DoesNotContain(CardTypes.Nope, hand);
            });
            Assert.Equal(6, state.Deck.Concat(state.PlayerHands.Values.SelectMany(h => h)).Count(c => c == CardTypes.Defuse));
        }
    }

    [Fact]
    public void OrdinaryDrawAddsOnlyToHandAndPassesTurn()
    {
        var state = State(CardTypes.Shuffle);
        var user = state.TurnOrder[0];
        GameRules.Draw(state, user);
        Assert.Equal(new[] { CardTypes.Shuffle }, state.PlayerHands[user]);
        Assert.Empty(state.DiscardPile);
        Assert.Equal(1, state.CurrentTurnIndex);
    }

    [Fact]
    public void DefuseConsumesOneDrawWithoutKeepingOrDuplicatingKitten()
    {
        var state = State(CardTypes.ExplodingKitten);
        var user = state.TurnOrder[0];
        state.PlayerHands[user].Add(CardTypes.Defuse);
        state.PendingDraws = 2;
        GameRules.Draw(state, user);
        Assert.True(state.Defused);
        Assert.Empty(state.PlayerHands[user]);
        Assert.Equal(new[] { CardTypes.ExplodingKitten }, state.Deck);
        Assert.Equal(new[] { CardTypes.Defuse }, state.DiscardPile);
        Assert.Equal(1, state.PendingDraws);
        Assert.Equal(0, state.CurrentTurnIndex);
        GameRules.Draw(state, user);
        Assert.True(state.IsEliminated(user));
        Assert.Equal(GameStatus.Finished, state.Status);
        Assert.Equal(state.TurnOrder[1], state.WinnerUserId);
    }

    [Fact]
    public void EliminationSkipsPreviouslyEliminatedPlayersAndResetsDebt()
    {
        var state = State(CardTypes.ExplodingKitten, 4);
        var user = state.TurnOrder[0];
        state.EliminatedUserIds.Add(state.TurnOrder[1]);
        state.PendingDraws = 4;
        state.PlayerHands[user].Add(CardTypes.Attack);
        GameRules.Draw(state, user);
        Assert.True(state.IsEliminated(user));
        Assert.Empty(state.PlayerHands[user]);
        Assert.Equal(2, state.CurrentTurnIndex);
        Assert.Equal(1, state.PendingDraws);
        Assert.Equal(GameStatus.Playing, state.Status);
    }

    [Fact]
    public void SkipPaysOneOwedDrawAndAttackStacksRemainingDebt()
    {
        var state = State(CardTypes.Shuffle);
        var user = state.TurnOrder[0];
        state.PlayerHands[user] = [CardTypes.Skip, CardTypes.Attack];
        state.PendingDraws = 3;
        GameRules.Play(state, user, CardTypes.Skip, null);
        Assert.Equal(2, state.PendingDraws);
        Assert.Equal(0, state.CurrentTurnIndex);
        GameRules.Play(state, user, CardTypes.Attack, null);
        Assert.Equal(4, state.PendingDraws);
        Assert.Equal(1, state.CurrentTurnIndex);
    }

    [Fact]
    public void FutureIsPrivateResultInExactDrawOrderAndKeepsTurn()
    {
        var state = State(CardTypes.Skip);
        state.Deck = [CardTypes.Attack, CardTypes.Defuse, CardTypes.Shuffle, CardTypes.Skip];
        var user = state.TurnOrder[0];
        state.PlayerHands[user].Add(CardTypes.SeeTheFuture);
        var result = GameRules.Play(state, user, CardTypes.SeeTheFuture, null);
        Assert.Equal(new[] { CardTypes.Skip, CardTypes.Shuffle, CardTypes.Defuse }, result.FutureCards);
        Assert.Equal(4, state.Deck.Count);
        Assert.Equal(0, state.CurrentTurnIndex);
    }

    [Fact]
    public void InvalidFavorDoesNotConsumeCardOrChangeState()
    {
        var state = State(CardTypes.Skip);
        var user = state.TurnOrder[0];
        state.PlayerHands[user].Add(CardTypes.Favor);
        Assert.Throws<GameError>(() => GameRules.Play(state, user, CardTypes.Favor, user));
        Assert.Throws<GameError>(() => GameRules.Play(state, user, CardTypes.Favor, state.TurnOrder[1]));
        Assert.Contains(CardTypes.Favor, state.PlayerHands[user]);
        Assert.Empty(state.DiscardPile);
    }

    [Fact]
    public void FavorTransfersExactlyOneCard()
    {
        var state = State(CardTypes.Skip);
        var user = state.TurnOrder[0]; var other = state.TurnOrder[1];
        state.PlayerHands[user] = [CardTypes.Favor];
        state.PlayerHands[other] = [CardTypes.Attack];
        GameRules.Play(state, user, CardTypes.Favor, other);
        Assert.Equal(new[] { CardTypes.Attack }, state.PlayerHands[user]);
        Assert.Empty(state.PlayerHands[other]);
        Assert.Equal(new[] { CardTypes.Favor }, state.DiscardPile);
    }

    [Fact]
    public void OutsidersAndOutOfTurnActionsCannotMutateGame()
    {
        var state = State(CardTypes.Shuffle);
        Assert.Throws<GameError>(() => GameRules.Draw(state, Guid.NewGuid()));
        Assert.Throws<GameError>(() => GameRules.Draw(state, state.TurnOrder[1]));
        Assert.Single(state.Deck);
    }

    private static GameState State(string top, int count = 2)
    {
        var players = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
        return new GameState { RoomId = Guid.NewGuid(), TurnOrder = players,
            PlayerHands = players.ToDictionary(id => id, _ => new List<string>()),
            Deck = [top], Status = GameStatus.Playing };
    }
}
