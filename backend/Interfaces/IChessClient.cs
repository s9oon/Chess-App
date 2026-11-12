namespace backend.Interfaces;

// Methods the SERVER calls on the FRONTEND (server -> client).
// The frontend listens for these by name, e.g. connection.on("MoveMade", (uci, fen) => ...)
public interface IChessClient
{
    Task GameStarted(string gameId, string fen);
    Task MoveMade(string uci, string fen);
    Task GameOver(string reason);
    Task OpponentLeft();
}
