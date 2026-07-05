using System.Text.Json.Serialization;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Core;

namespace AethericForge.Runtime.Models.Knowledge.Core;

public sealed record KnowledgeDescriptor : IKnowledgeDescriptor
{
    private const int MaxTitleLength = 256;
    private const int MaxAbstractLength = 2048;
    private const int MaxSummaryLength = 4096;
    private const int MaxDescriptionLength = 8192;

    [JsonConstructor]
    public KnowledgeDescriptor(
        string title,
        string? @abstract,
        string? summary,
        string? description)
    {
        Title = NormalizeRequired(title, nameof(title), MaxTitleLength);
        Abstract = NormalizeOptional(@abstract, nameof(@abstract), MaxAbstractLength);
        Summary = NormalizeOptional(summary, nameof(summary), MaxSummaryLength);
        Description = NormalizeOptional(description, nameof(description), MaxDescriptionLength);
    }

    public KnowledgeDescriptor(string title)
        : this(title, @abstract: null, summary: null, description: null)
    {
    }

    public string Title { get; }
    public string? Abstract { get; }
    public string? Summary { get; }
    public string? Description { get; }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return NormalizeLength(value.Trim(), parameterName, maxLength);
    }

    private static string? NormalizeOptional(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeLength(value.Trim(), parameterName, maxLength);
    }

    private static string NormalizeLength(string value, string parameterName, int maxLength)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Value must be {maxLength} characters or fewer.");
        }

        return value;
    }
}
