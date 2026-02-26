namespace AethericForge.Runtime.Bus;

public sealed class TransportEnvelope
{
    public string RoutingKey { get; init; } = default!;
    public string MessageType { get; init; } = default!;
    public string PayloadJson { get; init; } = default!;
    public string Kind { get; init; } = default!;
    public string Service { get; init; } = default!;
    public string Verb { get; init; } = default!;
}
