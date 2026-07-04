using System.Text.Json;
using System.Text.Json.Serialization;

namespace AethericForge.Runtime.Bus.Abstractions;

/// <summary>
/// Custom JSON converter for the abstract Envelope class.
/// Handles deserialization by reconstructing the appropriate Envelope<T> instance
/// based on the UntypedPayload type information.
/// </summary>
public sealed class EnvelopeJsonConverter : JsonConverter<Envelope>
{
    public override Envelope? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Extract basic properties
        var kind = (EnvelopeKind)root.GetProperty("Kind").GetInt32();
        var id = Guid.Parse(root.GetProperty("Id").GetString()!);
        var version = root.GetProperty("Version").GetInt32();
        var service = root.GetProperty("Service").GetString() ?? string.Empty;
        var verb = root.TryGetProperty("Verb", out var verbProp) && verbProp.ValueKind != JsonValueKind.Null
            ? verbProp.GetString()
            : null;
        var topic = root.TryGetProperty("Topic", out var topicProp) && topicProp.ValueKind != JsonValueKind.Null
            ? topicProp.GetString()
            : null;

        // Extract RouteKey
        var routeKeyObj = root.GetProperty("RouteKey");
        var rkKind = (EnvelopeKind)routeKeyObj.GetProperty("Kind").GetInt32();
        var rkService = routeKeyObj.GetProperty("Service").GetString() ?? string.Empty;
        var rkVerb = routeKeyObj.TryGetProperty("Verb", out var rkVerbProp) && rkVerbProp.ValueKind != JsonValueKind.Null
            ? rkVerbProp.GetString()
            : null;
        var rkTopic = routeKeyObj.TryGetProperty("Topic", out var rkTopicProp) && rkTopicProp.ValueKind != JsonValueKind.Null
            ? rkTopicProp.GetString()
            : null;

        var routeKey = new RouteKey(rkKind, rkService, rkVerb, rkTopic);

        // Extract metadata
        var meta = new Dictionary<string, string>();
        if (root.TryGetProperty("Meta", out var metaProp) && metaProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in metaProp.EnumerateObject())
            {
                meta[item.Name] = item.Value.GetString() ?? string.Empty;
            }
        }

        Guid? correlationId = null;
        if (root.TryGetProperty("CorrelationId", out var corrProp) && corrProp.ValueKind != JsonValueKind.Null)
        {
            correlationId = Guid.Parse(corrProp.GetString()!);
        }

        Guid? causationId = null;
        if (root.TryGetProperty("CausationId", out var causProp) && causProp.ValueKind != JsonValueKind.Null)
        {
            causationId = Guid.Parse(causProp.GetString()!);
        }

        var timestamp = root.TryGetProperty("Timestamp", out var tsProp)
            ? DateTimeOffset.Parse(tsProp.GetString()!)
            : DateTimeOffset.UtcNow;

        // Extract the payload - try both "UntypedPayload" and "Payload"
        object? payload = null;
        Type? payloadType = null;

        if (root.TryGetProperty("UntypedPayload", out var untypedPayload))
        {
            // Try to infer type from the payload structure or use a default
            payloadType = typeof(object);
            payload = JsonSerializer.Deserialize<object>(untypedPayload.GetRawText(), options);
        }
        else if (root.TryGetProperty("Payload", out var typedPayload))
        {
            payloadType = typeof(object);
            payload = JsonSerializer.Deserialize<object>(typedPayload.GetRawText(), options);
        }

        if (payload == null)
            throw new JsonException("No payload found in envelope JSON");

        // Create Envelope<object> and return as base Envelope
        var envelopeType = typeof(Envelope<>).MakeGenericType(payload.GetType());
        var instance = Activator.CreateInstance(
            envelopeType,
            kind,
            payload,
            meta,
            routeKey,
            id,
            correlationId,
            causationId,
            timestamp) as Envelope;

        return instance ?? throw new JsonException("Failed to instantiate Envelope<T>");
    }

    public override void Write(Utf8JsonWriter writer, Envelope value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object)value, options);
    }
}
