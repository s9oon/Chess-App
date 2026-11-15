namespace backend.Chess;

public enum Color { White, Black }

public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King }

public readonly record struct Piece(Color Color, PieceType Type)
{
    // FEN letters: uppercase = white, lowercase = black.  P N B R Q K
    public char ToFenChar()
    {
        char c = Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => throw new ArgumentOutOfRangeException(),
        };
        return Color == Color.White ? char.ToUpperInvariant(c) : c;
    }

    public static Piece FromFenChar(char c)
    {
        Color color = char.IsUpper(c) ? Color.White : Color.Black;
        PieceType type = char.ToLowerInvariant(c) switch
        {
            'p' => PieceType.Pawn,
            'n' => PieceType.Knight,
            'b' => PieceType.Bishop,
            'r' => PieceType.Rook,
            'q' => PieceType.Queen,
            'k' => PieceType.King,
            _ => throw new FormatException($"'{c}' is not a piece letter."),
        };
        return new Piece(color, type);
    }
}

public static class ColorExtensions
{
    public static Color Opponent(this Color color) => color == Color.White ? Color.Black : Color.White;
}
