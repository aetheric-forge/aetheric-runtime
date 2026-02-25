using System.Text.Json;

namespace AethericForge.Runtime.Bus.Abstractions;

public sealed class Envelope
{
    public int Version { get; init; } = 1;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Kind { get; init; } = default!; // request | response | event | error

    public string? Service { get; init; }

    public string? Verb { get; init; }

    public string? Topic { get; init; }

    public Guid? CorrelationId { get; init; }

    public Guid? CausationId { get; init; }

    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public Dictionary<string, string>? Meta { get; init; }

    public JsonElement Payload { get; init; }
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
            case "request":
                if (string.IsNullOrWhiteSpace(e.Service) || string.IsNullOrWhiteSpace(e.Verb))
                    throw new InvalidOperationException("Request requires Service and Verb.");
                break;

            case "response":
            case "error":
                if (e.CorrelationId is null)
                    throw new InvalidOperationException($"{e.Kind} requires CorrelationId.");
                break;

            case "event":
                if (string.IsNullOrWhiteSpace(e.Topic))
                    throw new InvalidOperationException("Event requires Topic.");
                break;

            default:
                throw new InvalidOperationException($"Unknown envelope kind: {e.Kind}");
        }
    }
}