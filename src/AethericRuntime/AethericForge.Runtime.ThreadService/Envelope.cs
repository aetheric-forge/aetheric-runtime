using System.Text.Json.Serialization;

namespace AethericForge.Runtime.ThreadService;

public sealed class EnvelopeMeta
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("reply_channel")]
    public string ReplyChannel { get; set; } = "";

    [JsonPropertyName("correlation_id")]
    public string CorrelationId { get; set; } = "";

    [JsonPropertyName("routing_key")]
    public string RoutingKey { get; set; } = "";
}

public sealed class Envelope
{
    [JsonPropertyName("meta")]
    public EnvelopeMeta Meta { get; set; } = new();

    // Raw body as JsonElement – C# services can interpret as needed
    [JsonPropertyName("body")]
    public System.Text.Json.JsonElement Body { get; set; }
}
