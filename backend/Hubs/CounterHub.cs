using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace backend.Hubs;

public class CounterHub(IConnectionMultiplexer redis) : Hub
{
    public const string TallyKey = "counter";

    public async Task Count(int userId)
    {
        long count = await redis.GetDatabase().HashIncrementAsync(TallyKey, userId);
        await Clients.All.SendAsync("Counted", userId, count);
    }

    public async Task<Dictionary<int, long>> GetTallies()
    {
        HashEntry[] entries = await redis.GetDatabase().HashGetAllAsync(TallyKey);
        return entries.ToDictionary(e => (int)e.Name, e => (long)e.Value);
    }
}
