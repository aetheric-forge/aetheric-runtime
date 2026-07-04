using System.Text.Json;

namespace AethericForge.Runtime.Bus.Abstractions;

public readonly record struct TransportEnvelope(
        string MessageType,
        EnvelopeKind Kind,
        RouteKey RouteKey,
        Guid Id,
        int Version,
        DateTimeOffset Timestamp,
        Guid? CorrelationId,
        Guid? CausationId,
        ReadOnlyMemory<byte> Payload,
        Dictionary<string, string> Meta)
{
    public TransportEnvelope(Envelope envelope) :
        this(
                MessageType: envelope.UntypedPayload.GetType().AssemblyQualifiedName
                    ?? throw new InvalidOperationException($"unable to determine type of payload"),
                Kind: envelope.Kind,
                RouteKey: envelope.RouteKey,
                Id: envelope.Id,
                Version: envelope.Version,
                Timestamp: envelope.Timestamp,
                CorrelationId: envelope.CorrelationId,
                CausationId: envelope.CausationId,
                Payload: JsonSerializer.SerializeToUtf8Bytes(envelope.UntypedPayload, envelope.UntypedPayload.GetType()),
                Meta: new Dictionary<string, string>(envelope.Meta)
            )
    { }
}
