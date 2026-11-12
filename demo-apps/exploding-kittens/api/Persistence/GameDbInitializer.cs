namespace ExplodingKittens.Api.Persistence;

using Microsoft.EntityFrameworkCore;
using ExplodingKittens.Api.Game;

public static class GameDbInitializer
{
    public static async Task InitializeAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Exploding Kittens database đã sẵn sàng.");
    }
}
