using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

public class ChessHub : Hub
{
    //public async Task NewMove(string username, ChessMove move) =>
        //await Clients.All.SendAsync("Move Received: ", username, move);
}