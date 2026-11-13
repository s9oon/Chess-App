using StackExchange.Redis;
using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

// Redis layout:
//   match:{id}  hash  host, guest   (connection ids)
public class ChessHub(IConnectionMultiplexer redis) : Hub
{
    private static readonly TimeSpan MatchTtl = TimeSpan.FromHours(6);

    private IDatabase Db() { return redis.GetDatabase(); }

    // Makes a new match, seats the caller as host, returns the id to share.
    public async Task<string> CreateMatch()
    {
        string me = Context.ConnectionId;
        string matchId = Guid.NewGuid().ToString("N")[..8];
        string key = $"match:{matchId}";

        await Db().HashSetAsync(key, "host", me);
        await Db().KeyExpireAsync(key, MatchTtl);

        await Groups.AddToGroupAsync(me, matchId);
        return matchId;
    }

    // Seats the caller as guest in an existing match.
    public async Task JoinMatch(string matchId)
    {
        string me = Context.ConnectionId;
        string key = $"match:{matchId}";

        // Check everything BEFORE joining the group, so nobody can sneak in.
        RedisValue host = await Db().HashGetAsync(key, "host");
        if (host.IsNull)
            throw new HubException("Match not found.");
        if (host == me)
            throw new HubException("You can't join your own match.");

        // When.NotExists: only writes if "guest" is empty, returns false if someone already has it.
        bool seated = await Db().HashSetAsync(key, "guest", me, When.NotExists);
        if (!seated)
            throw new HubException("Match is full.");

        await Groups.AddToGroupAsync(me, matchId);
        await Clients.OthersInGroup(matchId).SendAsync("OpponentJoined");
    }
}
