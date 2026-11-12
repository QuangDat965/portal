namespace ExplodingKittens.Api.Game;

public static class CardTypes
{
    public const string ExplodingKitten = "exploding_kitten";
    public const string Defuse = "defuse";
    public const string Skip = "skip";
    public const string Attack = "attack";
    public const string Shuffle = "shuffle";
    public const string SeeTheFuture = "see_the_future";
    public const string Favor = "favor";
    public const string Nope = "nope";
}

public static class CardNames
{
    public static string Get(string type) => type switch
    {
        CardTypes.ExplodingKitten => "Mèo Nổ",
        CardTypes.Defuse => "Giải Cứu",
        CardTypes.Skip => "Bỏ Lượt",
        CardTypes.Attack => "Tấn Công",
        CardTypes.Shuffle => "Xáo Bài",
        CardTypes.SeeTheFuture => "Xem Tương Lai",
        CardTypes.Favor => "Yêu Cầu Bài",
        CardTypes.Nope => "Không",
        _ => type,
    };
}

public static class DeckBuilder
{
    public static List<string> Build()
    {
        var cards = new List<string>();

        Add(cards, CardTypes.ExplodingKitten, 4);
        Add(cards, CardTypes.Defuse, 6);
        Add(cards, CardTypes.Skip, 5);
        Add(cards, CardTypes.Attack, 4);
        Add(cards, CardTypes.Shuffle, 4);
        Add(cards, CardTypes.SeeTheFuture, 4);
        Add(cards, CardTypes.Favor, 4);
        Add(cards, CardTypes.Nope, 5);

        return cards;
    }

    private static void Add(List<string> cards, string type, int count)
    {
        for (var i = 0; i < count; i++) cards.Add(type);
    }
}
