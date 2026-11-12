using Microsoft.AspNetCore.SignalR;
using backend.Interfaces;

namespace backend.Hubs;

// Thin hub: call GameService, manage groups, notify clients.
// No Redis and no chess rules in here.
// Doesn't compile yet: GameService, Game and IChessClient still need to be written.
public class ChessHub(GameService games) : Hub<IChessClient>
{
    public async Task<string> CreateGame()
    {
        Game game = await games.Create(whitePlayer: Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, game.Id);
        return game.Id;
    }

    public async Task JoinGame(string gameId)
    {
        Game game = await games.Join(gameId, blackPlayer: Context.ConnectionId); // throws: not found / full / own game
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        await Clients.Group(gameId).GameStarted(gameId, game.Fen);
    }

    public async Task MakeMove(string gameId, string uci, string fen)
    {
        await games.MakeMove(gameId, Context.ConnectionId, uci, fen); // throws: not your turn / bad move
        await Clients.OthersInGroup(gameId).MoveMade(uci, fen);
    }

    public async Task Resign(string gameId)
    {
        await games.End(gameId);
        await Clients.OthersInGroup(gameId).GameOver("Opponent resigned.");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? gameId = await games.FindGameFor(Context.ConnectionId);
        if (gameId != null)
        {
            await Clients.OthersInGroup(gameId).OpponentLeft();
            await games.End(gameId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
