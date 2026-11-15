using TypedSignalR.Client;
using backend.DTO;

namespace backend.Interface;

[Hub]       // methods the browser calls on the server
public interface IChessHub
{
    Task<string> CreateMatch();                      // returns the match id to share
    Task<GameState> JoinMatch(string matchId);       // returns the board so the joiner can draw it straight away
    Task LeaveMatch(string matchId);
    Task MovePiece(string matchId, string uci);      // "e2e4"; throws HubException if it's illegal or not your turn
}

[Receiver]  // messages the server sends to the browser
public interface IChessClient
{
    Task OpponentJoined();
    Task OpponentLeft();
    Task GameUpdated(GameState state);               // after every move, to both players
    Task MatchEnded();
}
