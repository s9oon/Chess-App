using Microsoft.ML.OnnxRuntime.Tensors;

namespace backend.Chess.Engine;

// Board -> the model's input. Port of Chess-Engine/src/encoding/board.py, must stay identical to it.
//
// Shape (1, 8, 8, 18), float32, indexed [batch, rank, file, plane]; rank 0 = rank 1, file 0 = the a-file
//   planes 0-5:   white pawn, knight, bishop, rook, queen, king
//   planes 6-11:  black pawn, knight, bishop, rook, queen, king
//   plane 12:     side to move (all 1s if white)
//   planes 13-16: castling rights (white kingside, white queenside, black kingside, black queenside)
//   plane 17:     en passant target square
public static class Encoding
{
    public const int Planes = 18;

    public static DenseTensor<float> Encode(Board board)
    {
        var planes = new DenseTensor<float>([1, 8, 8, Planes]);   // starts all 0

        // Pieces: PieceType is Pawn..King = 0..5, same as python-chess piece_type - 1
        for (int square = 0; square < 64; square++)
        {
            if (board[square] is not Piece piece)
                continue;

            int plane = (int)piece.Type + (piece.Color == Color.Black ? 6 : 0);
            planes[0, Square.Rank(square), Square.File(square), plane] = 1;
        }

        // Whole planes, so every conv patch can see them
        Fill(planes, 12, board.SideToMove == Color.White);
        Fill(planes, 13, board.Castling.HasFlag(CastlingRights.WhiteKingside));
        Fill(planes, 14, board.Castling.HasFlag(CastlingRights.WhiteQueenside));
        Fill(planes, 15, board.Castling.HasFlag(CastlingRights.BlackKingside));
        Fill(planes, 16, board.Castling.HasFlag(CastlingRights.BlackQueenside));

        // En passant: just the target square, if there is one
        if (board.EnPassant is int ep)
            planes[0, Square.Rank(ep), Square.File(ep), 17] = 1;

        return planes;
    }

    private static void Fill(DenseTensor<float> planes, int plane, bool on)
    {
        if (!on)
            return;
        for (int rank = 0; rank < 8; rank++)
            for (int file = 0; file < 8; file++)
                planes[0, rank, file, plane] = 1;
    }
}
