using StackExchange.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

[Authorize]
public class ChessHub(IConnectionMultiplexer redis) : Hub
{
    private static readonly TimeSpan MatchTtl = TimeSpan.FromHours(6);

    private IDatabase Db() { return redis.GetDatabase(); }

    // Redis: match:{id}  Player1, Player1State, Player2, Player2State, Counter
    //   PlayerN      = user id ("" = nobody)
    //   PlayerNState = "empty" | "occupied" | "reserved"
    //   Counter      = shared number both players change
    // Makes a new match with 2 empty seats, returns the id to share.
    public async Task<string> CreateMatch()
    {
        string matchId = Guid.NewGuid().ToString("N")[..8];
        string key = $"match:{matchId}";

        // Declare 2 seats: no owner yet ("") and state "empty".
        // JoinMatch fills a seat -> "occupied". Disconnecting -> "reserved".
        await Db().HashSetAsync(key,
        [
            new HashEntry("Player1", ""),
            new HashEntry("Player1State", "empty"),
            new HashEntry("Player2", ""),
            new HashEntry("Player2State", "empty"),
            new HashEntry("Counter", 0),
        ]);
        await Db().KeyExpireAsync(key, MatchTtl);

        return matchId;
    }

    // Puts the caller in the first empty seat. Calling it again when you already have a seat
    // (e.g. after a refresh) just marks your seat "occupied" again.
    // Returns the current counter so the joiner can show it straight away.
    public async Task<long> JoinMatch(string matchId)
    {
        string me = Context.UserIdentifier!;
        string key = $"match:{matchId}";

        Dictionary<string, string> match = await LoadMatch(key);

        string? mySeat = SeatOf(match, me);
        if (mySeat is not null)
        {
            await Db().HashSetAsync(key, $"{mySeat}State", "occupied");
            await NotifyOpponent(match, mySeat, "OpponentJoined");
            return (long)await Db().HashGetAsync(key, "Counter");
        }

        string seat;
        if (await TryTakeSeat(key, "Player1", me))
            seat = "Player1";
        else if (await TryTakeSeat(key, "Player2", me))
            seat = "Player2";
        else
            throw new HubException("Match is full.");

        await NotifyOpponent(match, seat, "OpponentJoined");
        return (long)await Db().HashGetAsync(key, "Counter");
    }

    // Takes the seat only if it's still "empty". The transaction makes the check + both writes
    // atomic: if two people join at once, only one of them gets true for this seat.
    private async Task<bool> TryTakeSeat(string key, string seat, string me)
    {
        ITransaction tx = Db().CreateTransaction();
        tx.AddCondition(Condition.HashEqual(key, $"{seat}State", "empty"));
        _ = tx.HashSetAsync(key,
        [
            new HashEntry(seat, me),
            new HashEntry($"{seat}State", "occupied"),
        ]);
        return await tx.ExecuteAsync();
    }

    // Frees the caller's seat so someone else can join.
    public async Task LeaveMatch(string matchId)
    {
        string me = Context.UserIdentifier!;
        string key = $"match:{matchId}";

        Dictionary<string, string> match = await LoadMatch(key);

        string mySeat = SeatOf(match, me) ?? throw new HubException("You're not in this match.");

        // Only empty the seat if it's still ours.
        ITransaction tx = Db().CreateTransaction();
        tx.AddCondition(Condition.HashEqual(key, mySeat, me));
        _ = tx.HashSetAsync(key,
        [
            new HashEntry(mySeat, ""),
            new HashEntry($"{mySeat}State", "empty"),
        ]);
        await tx.ExecuteAsync();

        await NotifyOpponent(match, mySeat, "OpponentLeft");
    }

    // Adds 1 to the match's counter and sends the new value to both players.
    // Returns the new value to the caller too.
    public async Task<long> IncrementCounter(string matchId)
    {
        string me = Context.UserIdentifier!;
        string key = $"match:{matchId}";

        Dictionary<string, string> match = await LoadMatch(key);
        if (SeatOf(match, me) is null)
            throw new HubException("You're not in this match.");

        // HINCRBY: Redis does the +1 itself in one command, so two clicks at the same
        // moment both count (read, add, write back could lose one).
        long value = await Db().HashIncrementAsync(key, "Counter");

        string[] players = Seats.Select(s => match[s]).Where(id => id != "").ToArray();
        await Clients.Users(players).SendAsync("CounterUpdated", value);

        return value;
    }

    // Server-only: private, so clients can't call it. The server decides when a match ends
    // (e.g. a reserved seat times out). Deletes the match and tells both players.
    private async Task EndMatch(string matchId)
    {
        string key = $"match:{matchId}";

        Dictionary<string, string> match = await LoadMatch(key);

        await Db().KeyDeleteAsync(key);

        string[] players = Seats.Select(s => match[s]).Where(id => id != "").ToArray();
        await Clients.Users(players).SendAsync("MatchEnded");
    }

    // ---- helpers ----

    private static readonly string[] Seats = ["Player1", "Player2"];

    // Reads the whole match hash into a dictionary: { "Player1": "...", "Player1State": "...", ... }
    private async Task<Dictionary<string, string>> LoadMatch(string key)
    {
        HashEntry[] entries = await Db().HashGetAllAsync(key);
        if (entries.Length == 0)
            throw new HubException("Match not found.");

        return entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
    }

    // Which seat this user owns, or null if none.
    private static string? SeatOf(Dictionary<string, string> match, string userId)
    {
        return Seats.FirstOrDefault(seat => match[seat] == userId);
    }

    // Sends a message to whoever owns the other seat (if anyone).
    // Clients.User reaches every open connection of that user, so it keeps working after a refresh.
    private async Task NotifyOpponent(Dictionary<string, string> match, string mySeat, string message)
    {
        string otherSeat = mySeat == "Player1" ? "Player2" : "Player1";
        string opponent = match[otherSeat];
        if (opponent != "")
            await Clients.User(opponent).SendAsync(message);
    }
}
