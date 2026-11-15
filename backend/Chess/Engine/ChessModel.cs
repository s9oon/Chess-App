using Tapper;

namespace backend.Chess.Engine;

// The bots you can play against. Each one needs a path in appsettings.json "ChessModels".
// [TranspilationSource] = the codegen also writes this as a TS enum (frontend/src/generated).
[TranspilationSource]
public enum ChessModel
{
    Goataro,
}
