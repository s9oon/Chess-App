namespace backend.Chess.Engine.MCTS;

// One position in the search tree. Port of Node in Chess-Engine/src/selfplay/mcts.py.
public sealed class Node(float prior)
{
    public float Prior { get; } = prior;           // policy head's probability for the move leading here
    public int Visits { get; set; }
    public float ValueSum { get; set; }             // from the view of the player who made the move leading here
    public Dictionary<Move, Node> Children { get; } = [];   // empty until expanded

    public float Q => Visits == 0 ? 0f : ValueSum / Visits;
}
