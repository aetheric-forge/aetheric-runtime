using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Util;

namespace AethericForge.Runtime.Hosting;

public sealed class MessageContext
{
    private readonly IReadOnlyDictionary<Type, object> _repos;

    public Envelope Envelope { get; }
    public string ServiceName { get; }
    public IBroker Broker { get; }
    public CancellationToken CancellationToken { get; }

    internal MessageContext(
        Envelope envelope,
        string serviceName,
        IBroker broker,
        IReadOnlyDictionary<Type, object> repos,
        CancellationToken cancellationToken)
    {
        Envelope = envelope;
        ServiceName = serviceName;
        Broker = broker;
        _repos = repos;
        CancellationToken = cancellationToken;
    }

    public TRepo GetRepo<TRepo>() where TRepo : class
    {
        if (_repos.TryGetValue(typeof(TRepo), out var repo))
            return (TRepo)repo;

        throw new InvalidOperationException($"No repo registered for {typeof(TRepo).FullName}.");
    }

    // Existing behavior: publish as a Request envelope.
    public Task PublishAsync<T>(T payload) where T : notnull
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        var routingKey = RoutingHelpers.ResolveRoutingKey(
            EnvelopeKind.Request,
            ServiceName,
            typeof(T).Name);

        var envelope = new Envelope<T>(routingKey, payload)
        {
            Kind = EnvelopeKind.Request,
            Service = ServiceName,
            Verb = typeof(T).Name
        };

        return Broker.PublishAsync(envelope, CancellationToken);
    }
}
