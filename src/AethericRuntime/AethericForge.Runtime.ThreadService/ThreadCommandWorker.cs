using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Model.Messages;

namespace AethericForge.Runtime.ThreadService;

public class ThreadCommandWorker(
    ILogger<ThreadCommandWorker> logger,
    IBroker broker
) : IHostedService
{
    private ILogger<ThreadCommandWorker> Logger { get; } = logger;
    private IBroker Broker { get; } = broker;

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        await Broker.Transport.Start();
        Broker.Route("thread.start", OnMessageAsync);
        Logger.LogInformation("ThreadCommandWorker subscribed to {RoutingKey}", "thread.start");
    }

    public async Task StopAsync(CancellationToken stoppingToken)
    {
        await Broker.Transport.Stop();
    }

    private async Task OnMessageAsync(Message msg)
    {
        try
        {
            Logger.LogInformation("ThreadCommandWorker received command: {RoutingKey}", msg.Type);
            await PublishThreadStartedEventAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling message");
        }
    }

    private async Task PublishThreadStartedEventAsync()
    {
        // Using bus abstraction with simple fire-and-forget emit. No payload required for now.
        const string routingKey = "thread.started";
        await Broker.Emit(routingKey, new Dictionary<string, object>
        {
            { "status", "started" },
            { "at", DateTimeOffset.UtcNow.ToString("O") }
        });
        Logger.LogInformation("Published event to {RoutingKey}", routingKey);
    }
}
