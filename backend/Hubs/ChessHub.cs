using TypedSignalR.Client;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

public class ChessHub(IConnectionMultiplexer redis) : Hub<IChessClient>, IChessHub
{
    //TODO
}