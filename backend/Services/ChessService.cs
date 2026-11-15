using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using backend.Chess;
using backend.Chess.Engine;
using backend.Interface;

namespace backend.Services;

// The Redis side of chess matches: who sits where, and the board.
// The hub calls this, then decides who to notify. Registered once (singleton) in Program.cs.
//
// Redis hash  chess:{id}
//   Player1, Player2           = user id ("" = nobody, "bot:<model>" = engine). Player1 plays White.
//   Player1State, Player2State = "empty" | "occupied" | "reserved"
//   Fen                        = the board
public class ChessService(IConnectionMultiplexer redis) : IChessService
{
    private static readonly TimeSpan MatchTtl = TimeSpan.FromHours(6);
    private static readonly string[] Seats = ["Player1", "Player2"];

    private IDatabase Db => redis.GetDatabase();
    private static string Key(string matchId) => $"chess:{matchId}";

    // New match, starting position. Returns the id to share.
    // With a model, the bot sits in Player2 (Black) as "occupied", so TryTakeSeat skips it
    // and the human who joins lands in Player1 (White).
    public async Task<string> Create(ChessModel? model = null)
    {
        string matchId = Guid.NewGuid().ToString("N")[..8];
        string key = Key(matchId);

        string player2 = model is ChessModel m ? BotId(m) : "";
        string player2State = model is null ? "empty" : "occupied";

        await Db.HashSetAsync(key,
        [
            new HashEntry("Player1", ""),
            new HashEntry("Player1State", "empty"),
            new HashEntry("Player2", player2),
            new HashEntry("Player2State", player2State),
            new HashEntry("Fen", Board.StartFen),
        ]);
        await Db.KeyExpireAsync(key, MatchTtl);   // Redis deletes the match 6 hours after it's created

        return matchId;
    }

    // The whole hash: { "Player1": "...", "Player1State": "...", "Fen": "...", ... }
    public async Task<Dictionary<string, string>> Load(string matchId)
    {
        HashEntry[] entries = await Db.HashGetAllAsync(Key(matchId));
        if (entries.Length == 0)
            throw new HubException("Match not found.");

        return entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
    }

    // Puts the user in the first empty seat. If they already have a seat (e.g. after a refresh),
    // marks it "occupied" again. Returns the match as it is after joining.
    public async Task<Dictionary<string, string>> Join(string matchId, string userId)
    {
        Dictionary<string, string> match = await Load(matchId);

        if (SeatOf(match, userId) is string mySeat)
            await Db.HashSetAsync(Key(matchId), $"{mySeat}State", "occupied");
        else if (!await TryTakeSeat(matchId, "Player1", userId) && !await TryTakeSeat(matchId, "Player2", userId))
            throw new HubException("Match is full.");

        return await Load(matchId);
    }

    // Frees the user's seat. Returns the opponent's id (to tell them), or null if there isn't one.
    public async Task<string?> Leave(string matchId, string userId)
    {
        Dictionary<string, string> match = await Load(matchId);
        string mySeat = SeatOf(match, userId) ?? throw new HubException("You're not in this match.");

        // Only empty the seat if it's still ours.
        ITransaction tx = Db.CreateTransaction();
        tx.AddCondition(Condition.HashEqual(Key(matchId), mySeat, userId));
        _ = tx.HashSetAsync(Key(matchId),
        [
            new HashEntry(mySeat, ""),
            new HashEntry($"{mySeat}State", "empty"),
        ]);
        await tx.ExecuteAsync();

        return OpponentOf(match, mySeat);
    }

    // Saves the board after a move, but only if it's still `fenBefore`.
    // If both players' requests arrive at once, only the first one saves; the other gets false.
    public async Task<bool> TrySaveBoard(string matchId, string fenBefore, string fenAfter)
    {
        ITransaction tx = Db.CreateTransaction();
        tx.AddCondition(Condition.HashEqual(Key(matchId), "Fen", fenBefore));
        _ = tx.HashSetAsync(Key(matchId), "Fen", fenAfter);
        return await tx.ExecuteAsync();
    }

    // ---- reading a loaded match ----
    // Static, so they're not in IChessService: call them as ChessService.SeatOf(...).
    // They only look at a dictionary you already have, they never touch Redis.
 
    // Which seat this user owns, or null if none.
    public static string? SeatOf(Dictionary<string, string> match, string userId)
    {
        return Seats.FirstOrDefault(seat => match[seat] == userId);
    }

    // User id in the other seat, or null if it's empty.
    public static string? OpponentOf(Dictionary<string, string> match, string mySeat)
    {
        string opponent = match[mySeat == "Player1" ? "Player2" : "Player1"];
        return opponent == "" ? null : opponent;
    }

    // "bot:Goataro". The prefix means it can never match a real user id.
    public static string BotId(ChessModel model) => $"bot:{model}";
    public static bool IsBot(string id) => id.StartsWith("bot:");

    // Player1 is White, Player2 is Black.
    public static Color ColorOf(string seat) => seat == "Player1" ? Color.White : Color.Black;

    // Both players' ids that are filled in, for sending to everyone in the match.
    public static string[] Players(Dictionary<string, string> match)
    {
        return Seats.Select(seat => match[seat]).Where(id => id != "").ToArray();
    }

    // ---- helpers ----

    // Takes the seat only if it's still "empty". The transaction makes the check + both writes
    // atomic: if two people join at once, only one of them gets true for this seat.
    private async Task<bool> TryTakeSeat(string matchId, string seat, string userId)
    {
        ITransaction tx = Db.CreateTransaction();
        tx.AddCondition(Condition.HashEqual(Key(matchId), $"{seat}State", "empty"));
        _ = tx.HashSetAsync(Key(matchId),
        [
            new HashEntry(seat, userId),
            new HashEntry($"{seat}State", "occupied"),
        ]);
        return await tx.ExecuteAsync();
    }
}
