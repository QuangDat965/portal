namespace ExplodingKittens.Api.Game;

/// <summary>Server-authoritative rules for the simplified 2–5 player game.</summary>
public static class GameRules
{
    public static GameState Create(Guid roomId, IReadOnlyList<Guid> players)
    {
        if (players.Count is < 2 or > 5 || players.Distinct().Count() != players.Count)
            throw new GameError("player_count");
        var deck = DeckBuilder.Build();
        deck.RemoveAll(c => c is CardTypes.ExplodingKitten or CardTypes.Nope);
        foreach (var _ in players) deck.Remove(CardTypes.Defuse);
        Shuffle(deck);
        var hands = players.ToDictionary(id => id, _ => new List<string> { CardTypes.Defuse });
        foreach (var id in players)
            for (var i = 0; i < 4; i++) hands[id].Add(Pop(deck));
        deck.AddRange(Enumerable.Repeat(CardTypes.ExplodingKitten, players.Count - 1));
        Shuffle(deck);
        return new GameState
        {
            RoomId = roomId, TurnOrder = players.ToList(), Deck = deck,
            PlayerHands = hands, Status = GameStatus.Playing,
            LastActionAt = DateTimeOffset.UtcNow
        };
    }

    public static PlayResult Draw(GameState state, Guid userId)
    {
        ValidateTurn(state, userId);
        if (state.Deck.Count == 0) throw new GameError("deck_empty");
        state.Exploded = false;
        state.Defused = false;
        state.EliminatedUserId = null;
        var card = Pop(state.Deck);
        if (card == CardTypes.ExplodingKitten)
        {
            state.Exploded = true;
            if (state.PlayerHands[userId].Remove(CardTypes.Defuse))
            {
                state.Defused = true;
                state.DiscardPile.Add(CardTypes.Defuse);
                state.Deck.Insert(Random.Shared.Next(state.Deck.Count + 1), card);
                CompleteDraw(state);
            }
            else
            {
                state.EliminatedUserIds.Add(userId);
                state.EliminatedUserId = userId;
                state.DiscardPile.Add(card);
                state.DiscardPile.AddRange(state.PlayerHands[userId]);
                state.PlayerHands[userId].Clear();
                var alive = state.TurnOrder.Where(id => !state.IsEliminated(id)).ToList();
                if (alive.Count == 1)
                {
                    state.Status = GameStatus.Finished;
                    state.WinnerUserId = alive[0];
                }
                else NextPlayer(state);
            }
        }
        else
        {
            state.PlayerHands[userId].Add(card);
            CompleteDraw(state);
        }
        state.LastActionAt = DateTimeOffset.UtcNow;
        return new PlayResult { Ok = true, State = state };
    }

    public static PlayResult Play(GameState state, Guid userId, string card, Guid? target)
    {
        ValidateTurn(state, userId);
        var hand = state.PlayerHands[userId];
        if (!hand.Contains(card)) throw new GameError("missing_card");
        List<string>? future = null;
        switch (card)
        {
            case CardTypes.Skip:
                CompleteDraw(state);
                break;
            case CardTypes.Attack:
                var draws = state.PendingDraws > 1 ? state.PendingDraws + 2 : 2;
                NextPlayer(state);
                state.PendingDraws = draws;
                break;
            case CardTypes.Shuffle:
                Shuffle(state.Deck);
                break;
            case CardTypes.SeeTheFuture:
                future = state.Deck.AsEnumerable().Reverse().Take(3).ToList();
                break;
            case CardTypes.Favor:
                if (target is null || target == userId || state.IsEliminated(target.Value)
                    || !state.PlayerHands.TryGetValue(target.Value, out var targetHand) || targetHand.Count == 0)
                    throw new GameError("invalid_target");
                var index = Random.Shared.Next(targetHand.Count);
                hand.Add(targetHand[index]);
                targetHand.RemoveAt(index);
                break;
            default:
                throw new GameError("unplayable_card");
        }
        hand.Remove(card);
        state.DiscardPile.Add(card);
        state.LastActionAt = DateTimeOffset.UtcNow;
        return new PlayResult { Ok = true, State = state, FutureCards = future };
    }

    private static void ValidateTurn(GameState state, Guid userId)
    {
        if (state.Status != GameStatus.Playing) throw new GameError("not_playing");
        if (!state.PlayerHands.ContainsKey(userId) || state.IsEliminated(userId))
            throw new GameError("not_player");
        if (state.TurnOrder[state.CurrentTurnIndex] != userId) throw new GameError("not_turn");
    }

    private static void CompleteDraw(GameState state)
    {
        if (--state.PendingDraws <= 0) NextPlayer(state);
    }

    private static void NextPlayer(GameState state)
    {
        do { state.CurrentTurnIndex = (state.CurrentTurnIndex + 1) % state.TurnOrder.Count; }
        while (state.IsEliminated(state.TurnOrder[state.CurrentTurnIndex]));
        state.PendingDraws = 1;
    }

    private static string Pop(List<string> cards)
    {
        var card = cards[^1];
        cards.RemoveAt(cards.Count - 1);
        return card;
    }

    private static void Shuffle(List<string> cards)
    {
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
}
