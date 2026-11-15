namespace backend.Chess.Engine;

// Model's policy output -> a move. Port of Chess-Engine/src/encoding/policy.py, must stay identical to it.
//
// 4672 scores, index = from_square * 73 + move_type   (from_square: a1 = 0 ... h8 = 63, same as Square)
//   move_type 0-55:  queen-style slides, direction * 7 + (distance - 1)
//                    rook/bishop/queen/king moves, pawn pushes + captures, castling, queen promotions
//   move_type 56-63: knight jumps
//   move_type 64-72: underpromotions, 64 + direction * 3 + piece
//                    direction: 0 = capture towards a-file, 1 = straight, 2 = capture towards h-file
//                    piece: 0 = knight, 1 = bishop, 2 = rook
public static class Decoding
{
    public const int MoveTypes = 73;
    public const int PolicySize = 64 * MoveTypes;   // 4672

    // (file step, rank step) per direction: N, NE, E, SE, S, SW, W, NW
    private static readonly (int, int)[] QueenDirections = [(0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1)];
    private static readonly (int, int)[] KnightJumps = [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];
    private static readonly PieceType[] Underpromotions = [PieceType.Knight, PieceType.Bishop, PieceType.Rook];

    // Move -> its slot in the 4672 scores
    public static int MoveToIndex(Move move)
    {
        int fileStep = Square.File(move.To) - Square.File(move.From);
        int rankStep = Square.Rank(move.To) - Square.Rank(move.From);
        int moveType;

        if (move.Promotion is PieceType p && Array.IndexOf(Underpromotions, p) is int piece and >= 0)
        {
            moveType = 64 + (fileStep + 1) * 3 + piece;
        }
        else if (Array.IndexOf(KnightJumps, (fileStep, rankStep)) is int jump and >= 0)
        {
            moveType = 56 + jump;
        }
        else
        {
            // Queen-style: split the step into a direction and a distance (1-7)
            int distance = Math.Max(Math.Abs(fileStep), Math.Abs(rankStep));
            int direction = Array.IndexOf(QueenDirections, (Math.Sign(fileStep), Math.Sign(rankStep)));
            moveType = direction * 7 + (distance - 1);
        }

        return move.From * MoveTypes + moveType;
    }

    // Raw policy scores -> probabilities over legal moves only (softmax), 0 for every illegal slot.
    // Same as evaluate() in Chess-Engine/src/selfplay/mcts.py. These are the priors MCTS uses.
    public static float[] LegalPriors(ReadOnlySpan<float> logits, Board board)
    {
        List<Move> legal = board.LegalMoves();
        if (legal.Count == 0)
            throw new InvalidOperationException("No legal moves, the game is over.");

        int[] indices = legal.Select(MoveToIndex).ToArray();
        float max = float.NegativeInfinity;   // subtract the max so exp() can't overflow
        foreach (int i in indices)
            max = MathF.Max(max, logits[i]);

        var priors = new float[PolicySize];
        float sum = 0f;
        foreach (int i in indices)
        {
            priors[i] = MathF.Exp(logits[i] - max);
            sum += priors[i];
        }
        foreach (int i in indices)
            priors[i] /= sum;

        return priors;
    }
}
