namespace backend.Chess.Engine.MCTS;

// appsettings.json  "Mcts": { "Simulations": 50, "CPuct": 1.5 }   (same defaults as Chess-Engine config.py)
public sealed class MctsSettings
{
    public int Simulations { get; set; } = 50;   // searches per move, each one is one model call
    public float CPuct { get; set; } = 1.5f;      // how much the search explores moves it hasn't checked much
}
