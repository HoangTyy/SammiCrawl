using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Channels;

namespace SammiCrawl.Backend.Services;

// Shared project: send invalidation only; records and secrets still use authenticated APIs.
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class LiveUpdatesHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var request = Context.GetHttpContext()!.Request;
        var origin = request.Headers.Origin.ToString();
        if (origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.Authority != request.Host.Value || uri.Scheme != request.Scheme))
        {
            Context.Abort();
            return;
        }
        await base.OnConnectedAsync();
    }
}

public sealed class LiveUpdates(IHubContext<LiveUpdatesHub> hub, ILogger<LiveUpdates> logger) : BackgroundService
{
    private readonly Channel<bool> changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    public void Notify() => changes.Writer.TryWrite(true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await changes.Reader.WaitToReadAsync(stoppingToken))
            {
                await Task.Delay(250, stoppingToken);
                while (changes.Reader.TryRead(out _)) { }
                try { await hub.Clients.All.SendAsync("DataChanged", cancellationToken: stoppingToken); }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(ex, "Live update delivery failed"); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
