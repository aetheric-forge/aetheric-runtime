namespace AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;

public interface IStagingMetadata
{
    string? ContentType { get; }
    long? ContentLength { get; }
    string? ETag { get; }
    DateTimeOffset? LastModifiedUtc { get; }
    TimeSpan? Expiration { get; }
    IReadOnlyDictionary<string, string> Attributes { get; }
}
