using Microsoft.AspNetCore.SignalR;

namespace EquiLink.Api.Hubs;

public class MarketDataHub : Hub
{
    public async Task SubscribeToSymbol(string symbol)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, symbol);
    }

    public async Task UnsubscribeFromSymbol(string symbol)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, symbol);
    }
}
