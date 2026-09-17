using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BetBuilder.Api.Hubs;

public class SyncNotifier : ISyncNotifier
{
    private readonly IHubContext<SyncHub> _hubContext;

    public SyncNotifier(IHubContext<SyncHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendProgressAsync(string message, CancellationToken ct = default)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveSyncProgress", message, cancellationToken: ct);
    }
}
