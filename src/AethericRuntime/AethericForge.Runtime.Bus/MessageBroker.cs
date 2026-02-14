using JetBrains.Annotations;
using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Model.Messages;

namespace AethericForge.Runtime.Bus;

public class MessageBroker(ITransport transport) : IBroker
{
    public ITransport Transport { get; } = transport;
    
    public Task Publish(Message msg) => Transport.Publish(msg);

    public Task Emit(string routingKey, IReadOnlyDictionary<string, object> payload, IReadOnlyDictionary<string, object>? meta = null)
    {
        var msg = new SimpleMessage(payload, routingKey, meta);
        return Transport.Publish(msg);
    }

    public void Route(string routingKey, MessageHandler handler)
    {
        // fire-and-forget subscribe; tests can await Start()
        Transport.Subscribe(routingKey, handler).GetAwaiter().GetResult();
    }

    private sealed class SimpleMessage(
        IReadOnlyDictionary<string, object> payload,
        string? type,
        IReadOnlyDictionary<string, object>? meta)
        : Message<Dictionary<string, object>>(new Dictionary<string, object>(payload), type)
    {
        [UsedImplicitly] public IReadOnlyDictionary<string, object>? Meta { get; } = meta;
    }
}
