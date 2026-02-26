using AethericForge.Runtime.Bus.Abstractions;

namespace AethericForge.Runtime.Util;

public class RoutingHelpers
{
    public static string ResolveRoutingKey(EnvelopeKind kind, string? serviceName, string queueName) =>
        kind switch
        {
            EnvelopeKind.Request => $"{serviceName}.{queueName}",
            EnvelopeKind.Response or EnvelopeKind.Error => $"reply.{queueName}",
            EnvelopeKind.Event => queueName,
            _ => throw new InvalidOperationException("Unknown routing kind."),
        };

    public static string ResolveRoutingKey(Envelope e) =>
        e.Kind switch
        {
            EnvelopeKind.Request => ResolveRoutingKey(e.Kind, e.Service, e.Verb!),
            EnvelopeKind.Response or EnvelopeKind.Error => ResolveRoutingKey(e.Kind, e.Service, e.Meta?["client_id"]!),
            EnvelopeKind.Event => ResolveRoutingKey(e.Kind, e.Service, e.Topic!),
            _ => throw new InvalidOperationException("Unknown envelope kind.")
        };
}
