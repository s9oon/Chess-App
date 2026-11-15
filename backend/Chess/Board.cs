namespace backend.Chess;

[Flags]
public enum CastlingRights
{
    None = 0,
    WhiteKingside = 1,
    WhiteQueenside = 2,
    BlackKingside = 4,
    BlackQueenside = 8,
}

public enum GameStatus { Ongoing, Checkmate, Stalemate }

// A chess position plus the rules. Knows nothing about SignalR or Redis:
// load it from FEN, ask for legal moves, apply one, save it back to FEN.
public class Board
{
    public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private readonly Piece?[] squares = new Piece?[64];   // index = square (a1 = 0 ... h8 = 63), null = empty

    public Color SideToMove { get; private set; }
    public CastlingRights Castling { get; private set; }
    public int? EnPassant { get; private set; }          // square a pawn can capture onto this move, or null
    public int HalfmoveClock { get; private set; }       // moves since the last capture or pawn move (50-move rule)
    public int FullmoveNumber { get; private set; } = 1; // goes up after Black moves

    public Piece? this[int square] => squares[square];

    private Board() { }

    public static Board Start() => FromFen(StartFen);

    // ---- FEN ----
    // "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
    //  pieces (rank 8 first, digits = empty squares) | side | castling | en passant | halfmove | fullmove

    public static Board FromFen(string fen)
    {
        string[] parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            throw new FormatException("FEN needs at least 4 fields.");

        Board board = new();

        string[] ranks = parts[0].Split('/');
        if (ranks.Length != 8)
            throw new FormatException("FEN needs 8 ranks.");

        for (int i = 0; i < 8; i++)
        {
            int rank = 7 - i;   // FEN starts at rank 8
            int file = 0;
            foreach (char c in ranks[i])
            {
                if (char.IsDigit(c))
                {
                    file += c - '0';
                }
                else
                {
                    if (file > 7)
                        throw new FormatException($"Rank {rank + 1} has too many squares.");
                    board.squares[Square.At(file, rank)] = Piece.FromFenChar(c);
                    file++;
                }
            }
            if (file != 8)
                throw new FormatException($"Rank {rank + 1} doesn't have 8 squares.");
        }

        board.SideToMove = parts[1] switch
        {
            "w" => Color.White,
            "b" => Color.Black,
            _ => throw new FormatException($"'{parts[1]}' is not a side (w or b)."),
        };

        if (parts[2] != "-")
        {
            foreach (char c in parts[2])
            {
                board.Castling |= c switch
                {
                    'K' => CastlingRights.WhiteKingside,
                    'Q' => CastlingRights.WhiteQueenside,
                    'k' => CastlingRights.BlackKingside,
                    'q' => CastlingRights.BlackQueenside,
                    _ => throw new FormatException($"'{c}' is not a castling right."),
                };
            }
        }

        board.EnPassant = parts[3] == "-" ? null : Square.Parse(parts[3]);

        if (parts.Length >= 5) board.HalfmoveClock = int.Parse(parts[4]);
        if (parts.Length >= 6) board.FullmoveNumber = int.Parse(parts[5]);

        return board;
    }

    public string ToFen()
    {
        var fen = new System.Text.StringBuilder();

        for (int rank = 7; rank >= 0; rank--)
        {
            int empty = 0;
            for (int file = 0; file < 8; file++)
            {
                if (squares[Square.At(file, rank)] is Piece p)
                {
                    if (empty > 0) { fen.Append(empty); empty = 0; }
                    fen.Append(p.ToFenChar());
                }
                else
                {
                    empty++;
                }
            }
            if (empty > 0) fen.Append(empty);
            if (rank > 0) fen.Append('/');
        }

        fen.Append(SideToMove == Color.White ? " w " : " b ");

        if (Castling == CastlingRights.None) fen.Append('-');
        if (Castling.HasFlag(CastlingRights.WhiteKingside)) fen.Append('K');
        if (Castling.HasFlag(CastlingRights.WhiteQueenside)) fen.Append('Q');
        if (Castling.HasFlag(CastlingRights.BlackKingside)) fen.Append('k');
        if (Castling.HasFlag(CastlingRights.BlackQueenside)) fen.Append('q');

        fen.Append(' ').Append(EnPassant is int ep ? Square.Name(ep) : "-");
        fen.Append(' ').Append(HalfmoveClock);
        fen.Append(' ').Append(FullmoveNumber);

        return fen.ToString();
    }

    public Board Clone()
    {
        Board copy = new()
        {
            SideToMove = SideToMove,
            Castling = Castling,
            EnPassant = EnPassant,
            HalfmoveClock = HalfmoveClock,
            FullmoveNumber = FullmoveNumber,
        };
        Array.Copy(squares, copy.squares, 64);
        return copy;
    }

    // ---- the public rules API ----

    public List<Move> LegalMoves()
    {
        // Generate every move the pieces can physically make, then throw away the ones
        // that leave our own king attacked (moving a pinned piece, ignoring check, ...).
        List<Move> legal = [];
        foreach (Move move in PseudoLegalMoves())
        {
            Board after = Clone();
            after.ApplyUnchecked(move);
            if (!after.IsAttacked(after.KingSquare(SideToMove), SideToMove.Opponent()))
                legal.Add(move);
        }
        return legal;
    }

    public bool IsLegal(Move move) => LegalMoves().Contains(move);

    // Plays a move. Throws if it isn't legal, so a bad move can never get into the position.
    public void Apply(Move move)
    {
        if (!IsLegal(move))
            throw new InvalidOperationException($"{move} is not a legal move.");
        ApplyUnchecked(move);
    }

    public bool InCheck() => IsAttacked(KingSquare(SideToMove), SideToMove.Opponent());

    // Only checkmate and stalemate for now. Not handled: 50-move rule (HalfmoveClock >= 100),
    // threefold repetition, insufficient material.
    public GameStatus Status()
    {
        if (LegalMoves().Count > 0) return GameStatus.Ongoing;
        return InCheck() ? GameStatus.Checkmate : GameStatus.Stalemate;
    }

    // Counts every move sequence of length `depth`. Compare against published numbers
    // (start position: 20, 400, 8902, 197281) to check move generation is correct.
    public long Perft(int depth)
    {
        if (depth == 0) return 1;

        long count = 0;
        foreach (Move move in LegalMoves())
        {
            Board after = Clone();
            after.ApplyUnchecked(move);
            count += after.Perft(depth - 1);
        }
        return count;
    }

    // ---- making a move ----

    private void ApplyUnchecked(Move move)
    {
        Piece moving = squares[move.From] ?? throw new InvalidOperationException($"No piece on {Square.Name(move.From)}.");
        Piece? captured = squares[move.To];
        bool isPawn = moving.Type == PieceType.Pawn;

        // En passant: the pawn being captured isn't on the target square, it's beside us.
        if (isPawn && move.To == EnPassant && Square.File(move.From) != Square.File(move.To) && captured is null)
        {
            int victim = Square.At(Square.File(move.To), Square.Rank(move.From));
            captured = squares[victim];
            squares[victim] = null;
        }

        squares[move.To] = move.Promotion is PieceType promoted ? new Piece(moving.Color, promoted) : moving;
        squares[move.From] = null;

        // Castling: the king moved two files, so move the rook too.
        if (moving.Type == PieceType.King && Math.Abs(Square.File(move.To) - Square.File(move.From)) == 2)
        {
            int rank = Square.Rank(move.From);
            bool kingside = Square.File(move.To) == 6;
            int rookFrom = Square.At(kingside ? 7 : 0, rank);
            int rookTo = Square.At(kingside ? 5 : 3, rank);
            squares[rookTo] = squares[rookFrom];
            squares[rookFrom] = null;
        }

        // Moving the king or a rook, or capturing a rook on its home square, loses those rights.
        Castling &= ~RightsLostOn(move.From);
        Castling &= ~RightsLostOn(move.To);

        // A pawn that moved two squares can be captured en passant on the square it skipped.
        EnPassant = isPawn && Math.Abs(Square.Rank(move.To) - Square.Rank(move.From)) == 2
            ? Square.At(Square.File(move.From), (Square.Rank(move.From) + Square.Rank(move.To)) / 2)
            : null;

        HalfmoveClock = isPawn || captured is not null ? 0 : HalfmoveClock + 1;
        if (SideToMove == Color.Black) FullmoveNumber++;
        SideToMove = SideToMove.Opponent();
    }

    private static CastlingRights RightsLostOn(int square) => square switch
    {
        4 => CastlingRights.WhiteKingside | CastlingRights.WhiteQueenside,   // e1
        7 => CastlingRights.WhiteKingside,                                   // h1
        0 => CastlingRights.WhiteQueenside,                                  // a1
        60 => CastlingRights.BlackKingside | CastlingRights.BlackQueenside,  // e8
        63 => CastlingRights.BlackKingside,                                  // h8
        56 => CastlingRights.BlackQueenside,                                 // a8
        _ => CastlingRights.None,
    };

    // ---- generating moves ----
    // Directions are (file change, rank change), so stepping off the edge is easy to catch.

    private static readonly (int df, int dr)[] KnightJumps = [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];
    private static readonly (int df, int dr)[] KingSteps = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)];
    private static readonly (int df, int dr)[] Diagonals = [(1, 1), (-1, 1), (-1, -1), (1, -1)];
    private static readonly (int df, int dr)[] Straights = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    // The square `df` files and `dr` ranks away, or -1 if that's off the board.
    private static int Offset(int square, int df, int dr)
    {
        int file = Square.File(square) + df;
        int rank = Square.Rank(square) + dr;
        return file is >= 0 and < 8 && rank is >= 0 and < 8 ? Square.At(file, rank) : -1;
    }

    private List<Move> PseudoLegalMoves()
    {
        List<Move> moves = [];
        for (int sq = 0; sq < 64; sq++)
        {
            if (squares[sq] is not Piece p || p.Color != SideToMove)
                continue;

            switch (p.Type)
            {
                case PieceType.Pawn: AddPawnMoves(sq, moves); break;
                case PieceType.Knight: AddSteps(sq, KnightJumps, moves); break;
                case PieceType.Bishop: AddSlides(sq, Diagonals, moves); break;
                case PieceType.Rook: AddSlides(sq, Straights, moves); break;
                case PieceType.Queen: AddSlides(sq, Diagonals, moves); AddSlides(sq, Straights, moves); break;
                case PieceType.King: AddSteps(sq, KingSteps, moves); AddCastling(moves); break;
            }
        }
        return moves;
    }

    // Knight and king: one jump in each direction, onto an empty square or an enemy piece.
    private void AddSteps(int from, (int df, int dr)[] directions, List<Move> moves)
    {
        foreach (var (df, dr) in directions)
        {
            int to = Offset(from, df, dr);
            if (to < 0) continue;
            if (squares[to] is Piece p && p.Color == SideToMove) continue;
            moves.Add(new Move(from, to));
        }
    }

    // Bishop, rook, queen: keep going in each direction until something is in the way.
    private void AddSlides(int from, (int df, int dr)[] directions, List<Move> moves)
    {
        foreach (var (df, dr) in directions)
        {
            int to = from;
            while (true)
            {
                to = Offset(to, df, dr);
                if (to < 0) break;

                if (squares[to] is Piece p)
                {
                    if (p.Color != SideToMove) moves.Add(new Move(from, to));   // capture, then stop
                    break;
                }
                moves.Add(new Move(from, to));
            }
        }
    }

    private void AddPawnMoves(int from, List<Move> moves)
    {
        int forward = SideToMove == Color.White ? 1 : -1;
        int startRank = SideToMove == Color.White ? 1 : 6;

        // One square forward, and two from the starting rank. Both need empty squares.
        int one = Offset(from, 0, forward);
        if (one >= 0 && squares[one] is null)
        {
            AddPawnMove(from, one, moves);

            if (Square.Rank(from) == startRank)
            {
                int two = Offset(from, 0, 2 * forward);
                if (squares[two] is null)
                    moves.Add(new Move(from, two));
            }
        }

        // Diagonal captures, including en passant.
        foreach (int df in (int[])[-1, 1])
        {
            int to = Offset(from, df, forward);
            if (to < 0) continue;

            if (squares[to] is Piece p && p.Color != SideToMove)
                AddPawnMove(from, to, moves);
            else if (to == EnPassant)
                moves.Add(new Move(from, to));
        }
    }

    // Reaching the last rank means promoting: that's 4 separate moves.
    private static void AddPawnMove(int from, int to, List<Move> moves)
    {
        int rank = Square.Rank(to);
        if (rank is 0 or 7)
        {
            moves.Add(new Move(from, to, PieceType.Queen));
            moves.Add(new Move(from, to, PieceType.Rook));
            moves.Add(new Move(from, to, PieceType.Bishop));
            moves.Add(new Move(from, to, PieceType.Knight));
        }
        else
        {
            moves.Add(new Move(from, to));
        }
    }

    // Castling needs: the right still available, the rook still there, the squares between empty,
    // and the king not in check, not passing through an attacked square, not landing on one.
    private void AddCastling(List<Move> moves)
    {
        Color us = SideToMove;
        Color them = us.Opponent();
        int rank = us == Color.White ? 0 : 7;
        int king = Square.At(4, rank);

        if (squares[king] != new Piece(us, PieceType.King) || IsAttacked(king, them))
            return;

        CastlingRights kingside = us == Color.White ? CastlingRights.WhiteKingside : CastlingRights.BlackKingside;
        CastlingRights queenside = us == Color.White ? CastlingRights.WhiteQueenside : CastlingRights.BlackQueenside;
        Piece rook = new(us, PieceType.Rook);

        if (Castling.HasFlag(kingside)
            && squares[Square.At(7, rank)] == rook
            && squares[Square.At(5, rank)] is null && squares[Square.At(6, rank)] is null
            && !IsAttacked(Square.At(5, rank), them) && !IsAttacked(Square.At(6, rank), them))
        {
            moves.Add(new Move(king, Square.At(6, rank)));
        }

        if (Castling.HasFlag(queenside)
            && squares[Square.At(0, rank)] == rook
            && squares[Square.At(3, rank)] is null && squares[Square.At(2, rank)] is null && squares[Square.At(1, rank)] is null
            && !IsAttacked(Square.At(3, rank), them) && !IsAttacked(Square.At(2, rank), them))
        {
            moves.Add(new Move(king, Square.At(2, rank)));
        }
    }

    // ---- attacks ----

    // Is `square` attacked by any piece of color `by`? Looks outward from the square:
    // "is there an enemy knight a knight-jump away? an enemy rook along a straight line? ..."
    private bool IsAttacked(int square, Color by)
    {
        // Pawns capture forward, so an attacking pawn sits one rank *behind* the square (from its side).
        int pawnRank = by == Color.White ? -1 : 1;
        foreach (int df in (int[])[-1, 1])
        {
            int from = Offset(square, df, pawnRank);
            if (from >= 0 && squares[from] == new Piece(by, PieceType.Pawn)) return true;
        }

        if (AttackedByStep(square, KnightJumps, new Piece(by, PieceType.Knight))) return true;
        if (AttackedByStep(square, KingSteps, new Piece(by, PieceType.King))) return true;
        if (AttackedBySlide(square, Diagonals, by, PieceType.Bishop)) return true;
        if (AttackedBySlide(square, Straights, by, PieceType.Rook)) return true;

        return false;
    }

    private bool AttackedByStep(int square, (int df, int dr)[] directions, Piece attacker)
    {
        foreach (var (df, dr) in directions)
        {
            int from = Offset(square, df, dr);
            if (from >= 0 && squares[from] == attacker) return true;
        }
        return false;
    }

    // The first piece along each line counts: if it's an enemy `slider` or queen, the square is attacked.
    private bool AttackedBySlide(int square, (int df, int dr)[] directions, Color by, PieceType slider)
    {
        foreach (var (df, dr) in directions)
        {
            int from = square;
            while (true)
            {
                from = Offset(from, df, dr);
                if (from < 0) break;
                if (squares[from] is Piece p)
                {
                    if (p.Color == by && (p.Type == slider || p.Type == PieceType.Queen)) return true;
                    break;
                }
            }
        }
        return false;
    }

    private int KingSquare(Color color)
    {
        Piece king = new(color, PieceType.King);
        for (int sq = 0; sq < 64; sq++)
            if (squares[sq] == king) return sq;
        throw new InvalidOperationException($"No {color} king on the board.");
    }
}
