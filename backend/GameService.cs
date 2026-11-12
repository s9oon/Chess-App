using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace backend;

public record Game(string Id, string White, string? Black, string Fen);

// Game rules + storage. No SignalR messaging in here: ChessHub calls this, then tells the clients.
// Errors are thrown as HubException so the message reaches the browser when called from the hub.
//
// Redis layout:
//   game:{id}        hash    white, black, fen
//   game:{id}:moves  list    every move in UCI, in order
//   conn:{connId}    string  which game this connection is in
//
// NOTE: moves aren't checked for legality yet. Only: your turn, UCI format, FEN flips the side to move.
public class GameService(IConnectionMultiplexer redis)
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private static readonly TimeSpan GameTtl = TimeSpan.FromHours(6);

    private IDatabase Db => redis.GetDatabase();

    private static string GameKey(string gameId) => $"game:{gameId}";
    private static string MovesKey(string gameId) => $"game:{gameId}:moves";
    private static string ConnKey(string playerId) => $"conn:{playerId}";

    // e2e4, e7e8q (promotion)
    private static readonly Regex UciMove = new("^[a-h][1-8][a-h][1-8][qrbn]?$", RegexOptions.Compiled);

    public async Task<Game> Create(string whitePlayer)
    {
        string gameId = Guid.NewGuid().ToString("N")[..8];

        await Db.HashSetAsync(GameKey(gameId),
        [
            new HashEntry("white", whitePlayer),
            new HashEntry("fen", StartFen),
        ]);
        await Db.KeyExpireAsync(GameKey(gameId), GameTtl);
        await Db.StringSetAsync(ConnKey(whitePlayer), gameId, GameTtl);

        return new Game(gameId, whitePlayer, null, StartFen);
    }

    public async Task<Game> Join(string gameId, string blackPlayer)
    {
        Game game = await Load(gameId) ?? throw new HubException("Game not found.");
        if (game.White == blackPlayer)
            throw new HubException("You can't join your own game.");

        // When.NotExists makes this atomic: if two people join at once, only one gets the seat.
        bool seated = await Db.HashSetAsync(GameKey(gameId), "black", blackPlayer, When.NotExists);
        if (!seated)
            throw new HubException("Game is full.");

        await Db.StringSetAsync(ConnKey(blackPlayer), gameId, GameTtl);

        return game with { Black = blackPlayer };
    }

    // uci: the move, e.g. "e2e4". fen: the position after the move.
    public async Task MakeMove(string gameId, string playerId, string uci, string fen)
    {
        if (!UciMove.IsMatch(uci))
            throw new HubException("Invalid move format.");

        Game game = await Load(gameId) ?? throw new HubException("Game not found.");
        if (game.Black is null)
            throw new HubException("Waiting for an opponent.");

        // Second field of a FEN is whose turn it is: "w" or "b".
        string turn = game.Fen.Split(' ')[1];
        string toMove = turn == "w" ? game.White : game.Black;
        if (playerId != toMove)
            throw new HubException("Not your turn.");

        string[] newFen = fen.Split(' ');
        if (newFen.Length != 6 || newFen[1] == turn)
            throw new HubException("Invalid position.");

        await Db.HashSetAsync(GameKey(gameId), "fen", fen);
        await Db.ListRightPushAsync(MovesKey(gameId), uci);
        await Db.KeyExpireAsync(MovesKey(gameId), GameTtl);
    }

    public async Task End(string gameId)
    {
        Game? game = await Load(gameId);
        if (game is null)
            return;

        await Db.KeyDeleteAsync(ConnKey(game.White));
        if (game.Black is not null)
            await Db.KeyDeleteAsync(ConnKey(game.Black));

        await Db.KeyDeleteAsync([GameKey(gameId), MovesKey(gameId)]);
    }

    public async Task<string?> FindGameFor(string playerId)
    {
        RedisValue gameId = await Db.StringGetAsync(ConnKey(playerId));
        return gameId.IsNull ? null : gameId.ToString();
    }

    private async Task<Game?> Load(string gameId)
    {
        HashEntry[] entries = await Db.HashGetAllAsync(GameKey(gameId));
        if (entries.Length == 0)
            return null;

        var fields = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
        return new Game(gameId, fields["white"], fields.GetValueOrDefault("black"), fields["fen"]);
    }
}
