namespace backend.Chess.Engine.MCTS;

// MCTS: thinks ahead from one position, then plays the move it visited most.
// Port of Tree in Chess-Engine/src/selfplay/mcts.py, without root noise (that's only for self-play).
//
// The tree never calls the model itself. One simulation is:
//   Board? leaf = tree.SelectLeaf();                 walk down to a position nobody has evaluated yet
//   (priors, value) = evaluate(leaf);                model call (ChessEngine does this)
//   tree.ExpandAndBackup(priors, value);             add the leaf's children, push its value back up
//
// Values are always for the side to move, same as the value head.
public sealed class Tree
{
    private readonly Board board;
    private readonly MctsSettings settings;
    private List<Node> path = [];
    private Board? leafBoard;

    public Node Root { get; } = new(prior: 1f);

    public Tree(Board board, MctsSettings settings)
    {
        if (board.Status() != GameStatus.Ongoing)
            throw new InvalidOperationException("Can't search a finished game.");
        this.board = board.Clone();
        this.settings = settings;
    }

    // Walk down picking the best-scoring child until reaching an unexpanded node.
    // Returns the leaf's board for the model, or null if the leaf was a finished game (already backed up).
    public Board? SelectLeaf()
    {
        Node node = Root;
        Board current = board.Clone();
        path = [node];

        while (node.Children.Count > 0)
        {
            float visitsSqrt = MathF.Sqrt(node.Visits);
            float bestScore = float.NegativeInfinity;
            Move bestMove = default;
            Node bestChild = null!;

            foreach ((Move move, Node child) in node.Children)
            {
                // PUCT: how good it looks + bonus for high prior and few visits
                float score = child.Q + settings.CPuct * child.Prior * visitsSqrt / (1 + child.Visits);
                if (score > bestScore)
                    (bestScore, bestMove, bestChild) = (score, move, child);
            }

            current.Apply(bestMove);
            node = bestChild;
            path.Add(node);
        }

        // Finished game: real result instead of the model
        if (Outcome(current) is float result)
        {
            Backup(result);
            return null;
        }

        leafBoard = current;
        return current;
    }

    // priors: 4672 probabilities (only legal moves non-zero), value: leaf eval for its side to move
    public void ExpandAndBackup(ReadOnlySpan<float> priors, float value)
    {
        Node leaf = path[^1];
        foreach (Move move in leafBoard!.LegalMoves())
            leaf.Children[move] = new Node(priors[Decoding.MoveToIndex(move)]);
        Backup(value);
    }

    // Most visited root move
    public Move BestMove()
    {
        return Root.Children.MaxBy(kv => kv.Value.Visits).Key;
    }

    // value is for the side to move at the leaf, flip it at every level going up
    private void Backup(float value)
    {
        for (int i = path.Count - 1; i >= 0; i--)
        {
            value = -value;   // node stores it from the view of whoever moved into it
            path[i].Visits++;
            path[i].ValueSum += value;
        }
    }

    // Result for the side to move, or null if the game goes on.
    // Checkmate = side to move lost. Python also counts repetition and insufficient material as draws;
    // Board doesn't track those, so only stalemate and the 50-move rule count here.
    // The root is never cut off by the 50-move rule, it always gets expanded so there's a move to play.
    private float? Outcome(Board position)
    {
        return position.Status() switch
        {
            GameStatus.Checkmate => -1f,
            GameStatus.Stalemate => 0f,
            _ when path.Count > 1 && position.HalfmoveClock >= 100 => 0f,
            _ => null,
        };
    }
}
