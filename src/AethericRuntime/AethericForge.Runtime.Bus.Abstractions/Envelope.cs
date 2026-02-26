namespace AethericForge.Runtime.Bus.Abstractions;

public enum EnvelopeKind
{
    Request,
    Response,
    Event,
    Error
}

public abstract class Envelope
{
    public string RoutingKey { get; }

    public Guid Id { get; init; } = Guid.NewGuid();
    public int Version { get; init; } = 1;

    public EnvelopeKind Kind { get; init; }

    public string? Service { get; init; }
    public string? Verb { get; init; }
    public string? Topic { get; init; }

    public Guid? CorrelationId { get; init; }
    public Guid? CausationId { get; init; }

    public Dictionary<string, string>? Meta { get; init; }

    public DateTimeOffset Timestamp { get; }

    protected Envelope(string routingKey)
    {
        RoutingKey = routingKey;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public abstract object UntypedPayload { get; }
}

public sealed class Envelope<T> : Envelope where T : notnull
{
    public T Payload { get; }

    public Envelope(string routingKey, T payload) : base(routingKey)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));

        // default to event type unless overridden in initializer
        Kind = EnvelopeKind.Event;
        Topic = routingKey;
    }

    public override object UntypedPayload => Payload;
}

public static class EnvelopeValidator
{
    public static void Validate(Envelope e)
    {
        if (e.Version != 1)
            throw new InvalidOperationException($"Unsupported envelope version: {e.Version}");

        if (e.Id == Guid.Empty)
            throw new InvalidOperationException("Envelope Id is required.");

        switch (e.Kind)
        {
            case EnvelopeKind.Request:
                if (string.IsNullOrWhiteSpace(e.Service) || string.IsNullOrWhiteSpace(e.Verb))
                    throw new InvalidOperationException("Request requires Service and Verb.");
                break;

            case EnvelopeKind.Response:
            case EnvelopeKind.Error:
                if (e.CorrelationId is null)
                    throw new InvalidOperationException($"{e.Kind} requires CorrelationId.");
                break;

            case EnvelopeKind.Event:
                if (string.IsNullOrWhiteSpace(e.Topic))
                    throw new InvalidOperationException("Event requires Topic.");
                break;

            default:
                throw new InvalidOperationException($"Unknown envelope kind: {e.Kind}");
        }
    }
}
