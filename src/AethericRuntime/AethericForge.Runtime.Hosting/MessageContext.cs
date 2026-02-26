using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Util;

namespace AethericForge.Runtime.Hosting;

public sealed class MessageContext
{
    public Envelope Envelope { get; }
    public string ServiceName { get; }
    public IBroker Broker { get; }
    public CancellationToken CancellationToken { get; }

    internal MessageContext(
        Envelope envelope,
        string serviceName,
        IBroker broker,
        CancellationToken cancellationToken)
    {
        Envelope = envelope;
        ServiceName = serviceName;
        Broker = broker;
        CancellationToken = cancellationToken;
    }

    public Task PublishAsync<T>(T payload) where T : notnull
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        var routingKey = RoutingHelpers.ResolveRoutingKey(
            EnvelopeKind.Request,
            ServiceName,
            typeof(T).Name);

        var envelope = new Envelope<T>(routingKey, payload);

        return Broker.PublishAsync(envelope, CancellationToken);
    }
}
