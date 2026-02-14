using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Model.Messages;

namespace AethericForge.Runtime.LogService;

public class LogCommandWorker(
    ILogger<LogCommandWorker> logger,
    IBroker broker
) : IHostedService
{
    private const string CommandRoutingKey = "log.write";
    private const string EventRoutingKey = "log.written";

    private ILogger<LogCommandWorker> Logger { get; } = logger;
    private IBroker Broker { get; } = broker;

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        await Broker.Transport.Start();
        Broker.Route(CommandRoutingKey, OnMessageAsync);
        Logger.LogInformation("LogCommandWorker subscribed to {RoutingKey}", CommandRoutingKey);
    }

    public async Task StopAsync(CancellationToken stoppingToken)
    {
        await Broker.Transport.Stop();
    }

    private async Task OnMessageAsync(Message msg)
    {
        try
        {
            var payload = ExtractPayload(msg);
            Logger.LogInformation("LogCommandWorker received command: {RoutingKey}", msg.Type);
            await PublishLogWrittenEventAsync(payload);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling message");
        }
    }

    private async Task PublishLogWrittenEventAsync(IReadOnlyDictionary<string, object> payload)
    {
        var body = new Dictionary<string, object>(payload)
        {
            ["status"] = "recorded",
            ["at"] = DateTimeOffset.UtcNow.ToString("O")
        };

        await Broker.Emit(EventRoutingKey, body);
        Logger.LogInformation("Published event to {RoutingKey}", EventRoutingKey);
    }

    private static IReadOnlyDictionary<string, object> ExtractPayload(Message msg) =>
        msg switch
        {
            Message<IReadOnlyDictionary<string, object>> ro => ro.Payload,
            Message<Dictionary<string, object>> dict => dict.Payload,
            _ => new Dictionary<string, object>()
        };
}
