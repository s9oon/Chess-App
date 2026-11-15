using backend.Chess.Engine;

namespace backend.Interface;

// What ChessHub needs from the match storage. ChessService implements it with Redis.
// Program.cs: AddSingleton<IChessService, ChessService>() -> asking for IChessService gives you a ChessService.
public interface IChessService
{
    Task<string> Create(ChessModel? model = null);   // model given -> a bot takes Player2
    Task<Dictionary<string, string>> Load(string matchId);
    Task<Dictionary<string, string>> Join(string matchId, string userId);
    Task<string?> Leave(string matchId, string userId);
    Task<bool> TrySaveBoard(string matchId, string fenBefore, string fenAfter);
}
