using System.Text.Json;
using AethericForge.Runtime.Bus.Abstractions;

namespace AethericForge.Runtime.Bus;

internal static class EnvelopeSerializer
{
    private static readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

    public static TransportEnvelope Serialize<T>(Envelope<T> envelope)
        where T : notnull
    {
        return new TransportEnvelope
        {
            RoutingKey = envelope.RoutingKey,
            MessageType = typeof(T).AssemblyQualifiedName!,
            PayloadJson = JsonSerializer.Serialize(envelope.Payload, _json),
            Kind = envelope.Kind.ToString(),
            Service = envelope.Service ?? "",
            Verb = envelope.Verb ?? ""
        };
    }

    public static Envelope<object> Deserialize(
            TransportEnvelope transport)
    {
        var type = Type.GetType(transport.MessageType)
            ?? throw new InvalidOperationException(
                $"Unable to resolve message type '{transport.MessageType}'.");

        var envelopeType = typeof(Envelope<>).MakeGenericType(type);

        var payload = JsonSerializer.Deserialize(
            transport.PayloadJson,
            type,
            _json) ?? throw new InvalidOperationException(
                $"Failed to deserialize payload for '{transport.MessageType}'.");

        // Create envelope instance
        var envelope = Activator.CreateInstance(
            envelopeType,
            transport.RoutingKey,
            payload,
            Enum.Parse<EnvelopeKind>(transport.Kind),
            transport.Service,
            transport.Verb) ??
            throw new NullReferenceException("Envelope<T> ctor returned null");

        return (Envelope<object>)envelope;
    }
}
