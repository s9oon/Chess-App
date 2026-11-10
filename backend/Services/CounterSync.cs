using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using backend.Data;
using backend.Hubs;

namespace backend.Services;

// Seeds the Redis tallies from Postgres on startup, then copies them back to Postgres every few seconds.
public class CounterSync(IServiceScopeFactory scopeFactory, IConnectionMultiplexer redis, ILogger<CounterSync> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool seeded = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!seeded)
                {
                    await SeedRedis(stoppingToken);
                    seeded = true;
                }
                else
                {
                    await FlushToPostgres(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Postgres or Redis is down; try again next tick instead of killing the app
                logger.LogWarning(ex, "Counter sync failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task SeedRedis(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IDatabase redisDb = redis.GetDatabase();

        foreach (var user in await db.Users.AsNoTracking().ToListAsync(ct))
            await redisDb.HashSetAsync(CounterHub.TallyKey, user.Id, user.Count, When.NotExists);
    }

    private async Task FlushToPostgres(CancellationToken ct)
    {
        HashEntry[] entries = await redis.GetDatabase().HashGetAllAsync(CounterHub.TallyKey);
        var tallies = entries.ToDictionary(e => (int)e.Name, e => (int)e.Value);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var user in await db.Users.ToListAsync(ct))
            if (tallies.TryGetValue(user.Id, out int count))
                user.Count = count;

        await db.SaveChangesAsync(ct);   // only writes rows whose Count actually changed
    }
}
