namespace backend.DTO;

// Everything a browser needs to draw the board. Returned by JoinMatch and sent to both players after every move.
//   Fen        = the whole position, e.g. "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1"
//   LegalMoves = every legal move for the side to move, in UCI: ["e7e5", "g8f6", ...]
//   Status     = "ongoing" | "checkmate" | "stalemate"
public record GameState(string Fen, string[] LegalMoves, string Status);
