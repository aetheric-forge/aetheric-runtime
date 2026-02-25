using AethericForge.Runtime.Bus.Abstractions;

namespace AethericForge.Runtime.Bus;

public class MessageBroker(ITransport transport) : IBroker
{
    public ITransport Transport { get; } = transport;
    
    public Task PublishAsync(Envelope envelope, CancellationToken ct = default) => Transport.PublishAsync(envelope, ct);

    public void Route(string routingKey, EnvelopeHandler handler)
    {
        // fire-and-forget subscribe; tests can await Start()
        Transport.SubscribeAsync(routingKey, handler).GetAwaiter().GetResult();
    }
}
