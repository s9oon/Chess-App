using TypedSignalR.Client;

namespace backend.Interface;

[Hub]       // methods the browser calls on the server
public interface ICounterHub
{
    Task<string> CreateMatch();
    Task<long> JoinMatch(string matchId);
    Task LeaveMatch(string matchId);
    Task<long> IncrementCounter(string matchId);
}

[Receiver]  // messages the server sends to the browser
public interface ICounterClient
{
    Task OpponentJoined();
    Task OpponentLeft();
    Task CounterUpdated(long value);
    Task MatchEnded();
}