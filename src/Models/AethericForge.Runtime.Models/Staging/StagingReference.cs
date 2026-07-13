using System.Text.Json.Serialization;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;

namespace AethericForge.Runtime.Models.Staging;

public sealed record StagingReference : IStagingReference
{
    [JsonConstructor]
    public StagingReference(string stage, string key)
    {
        Stage = NormalizeRequired(stage, nameof(stage));
        Key = NormalizeRequired(key, nameof(key));
    }

    public string Stage { get; }
    public string Key { get; }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
