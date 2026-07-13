namespace AethericForge.Runtime.Abstractions.Interfaces.Storage.Primitives;

public interface IStorageMetadata
{
    string? ContentType { get; }
    long? ContentLength { get; }
    string? ETag { get; }
    DateTimeOffset? LastModifiedUtc { get; }
    IReadOnlyDictionary<string, string> Attributes { get; }
}
