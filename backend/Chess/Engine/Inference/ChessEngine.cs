using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using backend.Chess.Engine.MCTS;
using backend.Interface;

namespace backend.Chess.Engine.Inference;

// Runs the .onnx models and the search: Board -> MCTS (Encoding -> model -> Decoding per simulation) -> Move.
//
// appsettings.json
//   "ChessModels": { "Goataro": "chessmodels/goataro.onnx" }   one entry per ChessModel, path relative to the app folder
//   "Mcts":        { "Simulations": 50, "CPuct": 1.5 }
public sealed class ChessEngine : IChessEngine, IDisposable
{
    // InferenceSession.Run is thread-safe, so one session per model is shared by every match
    private readonly Dictionary<ChessModel, InferenceSession> sessions = [];
    private readonly MctsSettings mcts;

    // Loads every model at startup, so a missing file fails on boot instead of mid-game
    public ChessEngine(IConfiguration config, IOptions<MctsSettings> mcts)
    {
        this.mcts = mcts.Value;
        foreach (ChessModel model in Enum.GetValues<ChessModel>())
        {
            string path = config[$"ChessModels:{model}"]
                ?? throw new InvalidOperationException($"appsettings has no ChessModels:{model} path.");
            sessions[model] = new InferenceSession(Path.Combine(AppContext.BaseDirectory, path));
        }
    }

    // Same loop as run_mcts() in Chess-Engine/src/selfplay/mcts.py, then the most visited move
    public Move ChooseMove(ChessModel model, Board board)
    {
        var tree = new Tree(board, mcts);
        for (int i = 0; i < mcts.Simulations; i++)
        {
            if (tree.SelectLeaf() is Board leaf)
            {
                (float[] priors, float value) = Evaluate(sessions[model], leaf);
                tree.ExpandAndBackup(priors, value);
            }
        }
        return tree.BestMove();
    }

    // One model call: priors over legal moves (4672, softmaxed) + value for the side to move (-1..1)
    private static (float[] Priors, float Value) Evaluate(InferenceSession session, Board board)
    {
        NamedOnnxValue input = NamedOnnxValue.CreateFromTensor("board", Encoding.Encode(board));

        using var results = session.Run([input], ["policy", "value"]);
        var logits = (DenseTensor<float>)results[0].AsTensor<float>();   // (1, 4672)
        var value = (DenseTensor<float>)results[1].AsTensor<float>();    // (1, 1)

        return (Decoding.LegalPriors(logits.Buffer.Span, board), value.Buffer.Span[0]);
    }

    public void Dispose()
    {
        foreach (InferenceSession session in sessions.Values)
            session.Dispose();
    }
}
