using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using backend.Interface;
using backend.DTO;
using backend.Chess;
using backend.Services;

namespace backend.Hubs;

[Authorize]
public class ChessHub(IChessService chess) : Hub<IChessClient>, IChessHub
{
    // New match with 2 empty seats and the starting board. Returns the id to share.
    public Task<string> CreateMatch()
    {
        return chess.Create();
    }

    // Takes a seat (or rejoins yours after a refresh), tells the opponent, and returns the board.
    public async Task<GameState> JoinMatch(string matchId)
    {
        string me = Context.UserIdentifier!;

        Dictionary<string, string> match = await chess.Join(matchId, me);   // throws "Match is full." / "Match not found."
        string mySeat = ChessService.SeatOf(match, me)!;                    // Join just seated us, so never null

        if (ChessService.OpponentOf(match, mySeat) is string opponent)
            await Clients.User(opponent).OpponentJoined();

        return ToGameState(Board.FromFen(match["Fen"]));
    }

    // Frees your seat and tells the opponent (if there is one).
    public async Task LeaveMatch(string matchId)
    {
        string me = Context.UserIdentifier!;

        string? opponent = await chess.Leave(matchId, me);   // throws "You're not in this match."
        if (opponent is not null)
            await Clients.User(opponent).OpponentLeft();
    }

    // Plays a move, saves the new board, and sends it to both players.
    public async Task MovePiece(string matchId, string uci)
    {
        string me = Context.UserIdentifier!;

        Dictionary<string, string> match = await chess.Load(matchId);
        string mySeat = ChessService.SeatOf(match, me) ?? throw new HubException("You're not in this match.");

        string fenBefore = match["Fen"];
        Board board = Board.FromFen(fenBefore);

        if (board.Status() != GameStatus.Ongoing)
            throw new HubException("The game is over.");
        if (board.SideToMove != ChessService.ColorOf(mySeat))
            throw new HubException("Not your turn.");

        Move move;
        try { move = Move.ParseUci(uci); }
        catch (FormatException) { throw new HubException($"'{uci}' is not a valid move."); }

        if (!board.IsLegal(move))
            throw new HubException($"{uci} is illegal.");

        board.Apply(move);

        // Someone else saved a move between our Load and now -> our board is stale.
        if (!await chess.TrySaveBoard(matchId, fenBefore, board.ToFen()))
            throw new HubException("The board changed, try again.");

        await Clients.Users(ChessService.Players(match)).GameUpdated(ToGameState(board));
    }

    // ---- helpers ----

    // Board -> what the browser needs to draw it. MovePiece will use this too, for GameUpdated.
    private static GameState ToGameState(Board board)
    {
        return new GameState(
            board.ToFen(),
            board.LegalMoves().Select(m => m.ToUci()).ToArray(),
            board.Status().ToString().ToLowerInvariant());   // Ongoing -> "ongoing"
    }
}
