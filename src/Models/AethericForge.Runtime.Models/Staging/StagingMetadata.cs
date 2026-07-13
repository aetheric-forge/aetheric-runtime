using System.Text.Json.Serialization;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;

namespace AethericForge.Runtime.Models.Staging;

public sealed record StagingMetadata : IStagingMetadata
{
    [JsonConstructor]
    public StagingMetadata(
        string? contentType = null,
        long? contentLength = null,
        string? eTag = null,
        DateTimeOffset? lastModifiedUtc = null,
        TimeSpan? expiration = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        if (contentLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contentLength), "Content length cannot be negative.");
        }

        ContentType = NormalizeOptional(contentType);
        ContentLength = contentLength;
        ETag = NormalizeOptional(eTag);
        LastModifiedUtc = lastModifiedUtc?.ToUniversalTime();
        Expiration = expiration;
        Attributes = NormalizeAttributes(attributes);
    }

    public string? ContentType { get; }
    public long? ContentLength { get; }
    public string? ETag { get; }
    public DateTimeOffset? LastModifiedUtc { get; }
    public TimeSpan? Expiration { get; }
    public IReadOnlyDictionary<string, string> Attributes { get; }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyDictionary<string, string> NormalizeAttributes(
        IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in attributes)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Attribute keys are required.", nameof(attributes));
            }

            normalized[key.Trim()] = value;
        }

        return normalized;
    }
}
