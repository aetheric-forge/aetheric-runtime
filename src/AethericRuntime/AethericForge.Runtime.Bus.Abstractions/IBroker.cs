namespace AethericForge.Runtime.Bus.Abstractions;

public delegate Task MessageHandler(object message, MessageContext ctx);

public class RouteKey
{
    public EnvelopeKind Kind { get; init; }
    public string Service { get; init; }
    public string? Verb { get; init; } = default;
    public string? Topic { get; init; } = default;

    public RouteKey(
            EnvelopeKind kind,
            string service,
            string? verb = default,
            string? topic = default)
    {
        switch (kind)
        {
            case EnvelopeKind.Request:
                if (verb is null) throw new ArgumentNullException(nameof(verb));
                break;
            // response and error are filtered on their respective queues by correlation id,
            // and service is known to be non-null
            case EnvelopeKind.Event:
                if (topic is null) throw new ArgumentNullException(nameof(topic));
                break;
        }

        Kind = kind;
        Service = service;
        Verb = verb;
        Topic = topic;
    }

    public string QueueName => Kind switch
    {
        EnvelopeKind.Request => $"{Service}.requests",
        EnvelopeKind.Response => $"{Service}.responses",
        EnvelopeKind.Error => $"{Service}.errors",
        EnvelopeKind.Event => $"{Service}.events",
        _ => throw new InvalidOperationException("invalid EnvelopeKind"),
    };
}

/**/
public interface IBroker
{
    Task PublishAsync(Envelope envelope, CancellationToken ct = default);
    void Route(RouteKey routeKey, EnvelopeHandler handler);
    ITransport Transport { get; }
}
