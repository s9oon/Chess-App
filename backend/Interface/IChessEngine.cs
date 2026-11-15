using backend.Chess;
using backend.Chess.Engine;

namespace backend.Interface;

// What ChessHub needs to let a bot play. ChessEngine implements it with ONNX Runtime.
// Program.cs: AddSingleton<IChessEngine, ChessEngine>() -> one engine, models loaded once at startup.
public interface IChessEngine
{
    // The move `model` plays in this position. Always legal. Throws if the game is over.
    Move ChooseMove(ChessModel model, Board board);
}
