namespace backend.Chess;

// Squares are ints 0..63:  a1 = 0, b1 = 1, ... h1 = 7, a2 = 8, ... h8 = 63
//   file (column a-h) = square % 8
//   rank (row 1-8)    = square / 8
public static class Square
{
    public static int File(int square) => square % 8;
    public static int Rank(int square) => square / 8;
    public static int At(int file, int rank) => rank * 8 + file;

    // "e2" -> 12
    public static int Parse(string name)
    {
        if (name.Length != 2 || name[0] < 'a' || name[0] > 'h' || name[1] < '1' || name[1] > '8')
            throw new FormatException($"'{name}' is not a square.");
        return At(name[0] - 'a', name[1] - '1');
    }

    // 12 -> "e2"
    public static string Name(int square) => $"{(char)('a' + File(square))}{(char)('1' + Rank(square))}";
}

// A move only says where a piece goes. Whether it's allowed is the Board's job.
// Castling is the king moving 2 squares ("e1g1"); en passant is a pawn moving diagonally
// onto the en-passant square. Board.Apply recognises both from the position.
public readonly record struct Move(int From, int To, PieceType? Promotion = null)
{
    // "e2e4" -> Move(12, 28)     "e7e8q" -> Move(52, 60, Queen)
    public static Move ParseUci(string uci)
    {
        if (uci.Length is not (4 or 5))
            throw new FormatException($"'{uci}' is not a UCI move.");

        int from = Square.Parse(uci[..2]);
        int to = Square.Parse(uci[2..4]);

        PieceType? promotion = null;
        if (uci.Length == 5)
        {
            promotion = uci[4] switch
            {
                'q' => PieceType.Queen,
                'r' => PieceType.Rook,
                'b' => PieceType.Bishop,
                'n' => PieceType.Knight,
                _ => throw new FormatException($"'{uci[4]}' is not a promotion piece."),
            };
        }

        return new Move(from, to, promotion);
    }

    // Move(12, 28) -> "e2e4"
    public string ToUci()
    {
        string uci = Square.Name(From) + Square.Name(To);
        if (Promotion is PieceType p)
            uci += new Piece(Color.Black, p).ToFenChar();   // lowercase letter
        return uci;
    }

    public override string ToString() => ToUci();
}
