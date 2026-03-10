using System.Text.Json;

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
    public string QueueName =>
        Kind switch
        {
            EnvelopeKind.Request => $"{Service!}.{Verb}",
            EnvelopeKind.Response => $"{Service!}.responses",
            EnvelopeKind.Event => $"{Service!}.responses",
            EnvelopeKind.Error => $"{Service!}.errors",
            _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
        };
    public Guid Id { get; protected init; } = Guid.NewGuid();
    public int Version { get; protected init; } = 1;

    public EnvelopeKind Kind { get; protected init; }
    public RouteKey RouteKey { get; protected init; } = new(EnvelopeKind.Request, "");

    public string Service { get; protected init; } = "";
    public string? Verb { get; protected init; }
    public string? Topic { get; protected init; }

    public Guid? CorrelationId { get; protected init; }
    public Guid? CausationId { get; protected init; }

    public Dictionary<string, string> Meta { get; protected init; } = new Dictionary<string, string>();

    public DateTimeOffset Timestamp { get; protected init; } = DateTimeOffset.UtcNow;

    public abstract object UntypedPayload { get; }

    public TransportEnvelope AsTransportEnvelope(JsonSerializerOptions? opts = null)
    {
        // Prefer the generic argument if this is Envelope<T>, otherwise fall back to runtime payload type.
        var messageType =
            GetType().BaseType?.IsGenericType == true &&
            GetType().BaseType!.GetGenericTypeDefinition().Name.StartsWith("Envelope`", StringComparison.Ordinal)
                ? GetType().BaseType!.GetGenericArguments()[0].AssemblyQualifiedName
                : UntypedPayload?.GetType().AssemblyQualifiedName;

        if (string.IsNullOrWhiteSpace(messageType))
            throw new InvalidOperationException("Cannot determine payload message type (T).");

        if (UntypedPayload is null)
            throw new InvalidOperationException("Cannot serialize null payload.");

        // Serialize as UTF-8 JSON to avoid an intermediate string allocation.
        // If you have source-gen contexts, you may want to route through those instead.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(UntypedPayload, UntypedPayload.GetType(), opts);

        // NOTE: this passes the dictionary reference through; if you need immutability, clone/copy it here.
        return new TransportEnvelope(
                MessageType: messageType,
                Kind: Kind,
                RouteKey: new RouteKey(EnvelopeKind.Request, Service, Verb, Topic),
                Id: Id,
                Version: Version,
                Timestamp: Timestamp,
                CorrelationId: CorrelationId,
                CausationId: CausationId,
                Payload: bytes,
                Meta: Meta);
    }
}

public sealed class Envelope<T> : Envelope where T : notnull
{
    public T Payload { get; }

    public Envelope(
            EnvelopeKind kind,
            T payload,
            Dictionary<string, string>? meta = null,
            RouteKey? routeKey = null,
            Guid? id = null,
            Guid? correlationId = null,
            Guid? causationId = null,
            DateTimeOffset? timestamp = null)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        Kind = kind;
        RouteKey = routeKey ?? throw new ArgumentNullException(nameof(routeKey));
        Id = Guid.NewGuid();
        CorrelationId = Guid.NewGuid();
        Meta = meta ?? new();
        RouteKey = routeKey;
        Id = id ?? Guid.NewGuid();
        CorrelationId = correlationId;
        CausationId = correlationId;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }

    public override object UntypedPayload => Payload;

    public static Envelope<T> FromTransportEnvelope(TransportEnvelope te)
    {
        var messageType = Type.GetType(te.MessageType) ??
            throw new InvalidOperationException($"parsed unknown message type '{te.MessageType}");
        if (messageType != typeof(T).GetType())
            throw new InvalidOperationException($"request to deserialize '{te.MessageType}' to '{typeof(T).Name}");
        var payload = JsonSerializer.Deserialize<T>(te.Payload.Span) ??
            throw new JsonException($"failed to deserialize object of type '{te.MessageType}");
        return new Envelope<T>
            (
                kind: te.Kind,
                payload: payload,
                meta: te.Meta,
                routeKey: te.RouteKey
            );
    }
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
