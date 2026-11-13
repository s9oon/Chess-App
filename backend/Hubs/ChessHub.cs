using StackExchange.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

[Authorize]
public class ChessHub(IConnectionMultiplexer redis) : Hub
{
    private static readonly TimeSpan MatchTtl = TimeSpan.FromHours(6);

    private IDatabase Db() { return redis.GetDatabase(); }

    // Redis: match:{id}  Player1, Player1State, Player2, Player2State
    //   PlayerN      = user id ("" = nobody)
    //   PlayerNState = "empty" | "occupied" | "reserved"
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
        ]);
        await Db().KeyExpireAsync(key, MatchTtl);

        return matchId;
    }

    // Puts the caller in the first empty seat. Calling it again when you already have a seat
    // (e.g. after a refresh) just marks your seat "occupied" again.
    public async Task JoinMatch(string matchId)
    {
        string me = Context.UserIdentifier!;
        string key = $"match:{matchId}";

        Dictionary<string, string> match = await LoadMatch(key);

        string? mySeat = SeatOf(match, me);
        if (mySeat is not null)
        {
            await Db().HashSetAsync(key, $"{mySeat}State", "occupied");
            await NotifyOpponent(match, mySeat, "OpponentJoined");
            return;
        }

        foreach (string seat in Seats)
        {
            // Take this seat only if it's still "empty". The transaction makes the check + both writes
            // atomic: if two people join at once, only one ExecuteAsync returns true for this seat.
            ITransaction tx = Db().CreateTransaction();
            tx.AddCondition(Condition.HashEqual(key, $"{seat}State", "empty"));
            _ = tx.HashSetAsync(key,
            [
                new HashEntry(seat, me),
                new HashEntry($"{seat}State", "occupied"),
            ]);

            if (await tx.ExecuteAsync())
            {
                await NotifyOpponent(match, seat, "OpponentJoined");
                return;
            }
        }

        throw new HubException("Match is full.");
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
