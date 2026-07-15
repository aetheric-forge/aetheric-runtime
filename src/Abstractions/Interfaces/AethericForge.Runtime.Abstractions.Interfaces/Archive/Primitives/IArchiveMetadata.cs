namespace AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;

public interface IArchiveMetadata
{
    string? ContentType { get; }
    long? ContentLength { get; }
    string? ETag { get; }
    DateTimeOffset? LastModifiedUtc { get; }
    IReadOnlyDictionary<string, string> Attributes { get; }
}
