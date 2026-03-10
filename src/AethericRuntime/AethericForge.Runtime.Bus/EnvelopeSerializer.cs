using System.Text;
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
        var json = JsonSerializer.Serialize<T>(envelope.Payload);

        return new TransportEnvelope
        {
            MessageType = typeof(T).Name,
            Meta = envelope.Meta,
            Payload = Encoding.UTF8.GetBytes(json),
        };
    }

    public static Envelope Deserialize(
            TransportEnvelope transport)
    {
        var type = Type.GetType(transport.MessageType)
            ?? throw new InvalidOperationException(
                $"Unable to resolve message type '{transport.MessageType}'.");

        var envelopeType = typeof(Envelope<>).MakeGenericType(type);

        var payload = JsonSerializer.Deserialize(
            transport.Payload.Span,
            type,
            _json) ?? throw new InvalidOperationException(
                $"Failed to deserialize payload for '{transport.MessageType}'.");

        // Create envelope instance
        var envelope = Activator.CreateInstance(envelopeType, payload) ??
           throw new NullReferenceException("Envelope<T> ctor returned null");

        return (Envelope)envelope;
    }
}
