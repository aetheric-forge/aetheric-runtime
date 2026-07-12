using AethericForge.Runtime.Abstractions.Interfaces.Post;

namespace AethericForge.Runtime.Models.Post;

public sealed record PostMetadata : IPostMetadata
{
    public PostMetadata(
        string? messageId = null,
        string? correlationId = null,
        string? causationId = null,
        DateTimeOffset? producedAtUtc = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        MessageId = NormalizeOptional(messageId) ?? Guid.NewGuid().ToString("N");
        CorrelationId = NormalizeOptional(correlationId);
        CausationId = NormalizeOptional(causationId);
        ProducedAtUtc = ToUtc(producedAtUtc ?? DateTimeOffset.UtcNow);
        Attributes = NormalizeAttributes(attributes);
    }

    public string MessageId { get; }
    public string? CorrelationId { get; }
    public string? CausationId { get; }
    public DateTimeOffset ProducedAtUtc { get; }
    public IReadOnlyDictionary<string, string> Attributes { get; }

    private static DateTimeOffset ToUtc(DateTimeOffset value)
    {
        return value.ToUniversalTime();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Dictionary<string, string> NormalizeAttributes(
        IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in attributes)
        {
            normalized[NormalizeRequired(key, nameof(attributes))] = value;
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
