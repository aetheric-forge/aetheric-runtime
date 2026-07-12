namespace AethericForge.Runtime.Abstractions.Interfaces.Post;

public interface IPostMetadata
{
    string MessageId { get; }
    string? CorrelationId { get; }
    string? CausationId { get; }
    DateTimeOffset ProducedAtUtc { get; }
    IReadOnlyDictionary<string, string> Attributes { get; }
}
