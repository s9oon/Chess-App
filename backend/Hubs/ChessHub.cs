using StackExchange.Redis;
using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

// Redis layout:
//   match:{id}  hash  Player1, Player2   (connection ids, "" = empty seat)
public class ChessHub(IConnectionMultiplexer redis) : Hub
{
    private static readonly TimeSpan MatchTtl = TimeSpan.FromHours(6);

    private IDatabase Db() { return redis.GetDatabase(); }

    // Makes a new match, seats the caller as Player1, returns the id to share.
    public async Task<string> CreateMatch()
    {
        string me = Context.ConnectionId;
        string matchId = Guid.NewGuid().ToString("N")[..8];
        string key = $"match:{matchId}";

        await Db().HashSetAsync(key,
        [
            new HashEntry("Player1", me),
            new HashEntry("Player2", ""),
        ]);
        await Db().KeyExpireAsync(key, MatchTtl);

        await Groups.AddToGroupAsync(me, matchId);
        return matchId;
    }

    // Seats the caller as Player2 in an existing match.
    public async Task JoinMatch(string matchId)
    {
        string me = Context.ConnectionId;
        string key = $"match:{matchId}";

        // Check everything BEFORE joining the group, so nobody can sneak in.
        if (!await Db().KeyExistsAsync(key))
            throw new HubException("Match not found.");

        // Take Player2 only if it's still "" (empty seat). The transaction makes the check + write
        // atomic: if two people join at once, only one ExecuteAsync returns true.
        ITransaction tx = Db().CreateTransaction();
        tx.AddCondition(Condition.HashEqual(key, "Player2", ""));
        _ = tx.HashSetAsync(key, "Player2", me);
        bool seated = await tx.ExecuteAsync();
        if (!seated)
            throw new HubException("Match is full.");

        await Groups.AddToGroupAsync(me, matchId);
        await Clients.OthersInGroup(matchId).SendAsync("OpponentJoined");
    }

    // Player2 leaving frees their seat so someone else can join.
    // Player1 leaving ends the match, since a match with no Player1 can't be ended by anyone.
    public async Task LeaveMatch(string matchId)
    {
        string me = Context.ConnectionId;
        string key = $"match:{matchId}";

        RedisValue[] seats = await Db().HashGetAsync(key, ["Player1", "Player2"]);
        if (seats[0].IsNull)
            throw new HubException("Match not found.");

        if (seats[0] == me)
        {
            await EndMatch(matchId);
            return;
        }

        if (seats[1] != me)
            throw new HubException("You're not in this match.");

        // Same trick as JoinMatch: only empty the seat if it's still ours.
        ITransaction tx = Db().CreateTransaction();
        tx.AddCondition(Condition.HashEqual(key, "Player2", me));
        _ = tx.HashSetAsync(key, "Player2", "");
        await tx.ExecuteAsync();

        await Groups.RemoveFromGroupAsync(me, matchId);
        await Clients.Group(matchId).SendAsync("OpponentLeft");
    }

    // Only Player1 can end the match. Deletes it and kicks everyone out of the group.
    public async Task EndMatch(string matchId)
    {
        string me = Context.ConnectionId;
        string key = $"match:{matchId}";

        RedisValue[] seats = await Db().HashGetAsync(key, ["Player1", "Player2"]);
        if (seats[0].IsNull)
            throw new HubException("Match not found.");
        if (seats[0] != me)
            throw new HubException("Only Player1 can end the match.");

        // Tell them before removing them from the group, otherwise the message has nobody to go to.
        await Clients.Group(matchId).SendAsync("MatchEnded");

        await Db().KeyDeleteAsync(key);

        foreach (RedisValue player in seats)
            if (player != "")
                await Groups.RemoveFromGroupAsync(player!, matchId);
    }
}
