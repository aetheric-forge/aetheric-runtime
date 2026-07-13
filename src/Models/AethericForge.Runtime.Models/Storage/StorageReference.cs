using System.Text.Json.Serialization;
using AethericForge.Runtime.Abstractions.Interfaces.Storage;
using AethericForge.Runtime.Abstractions.Interfaces.Storage.Primitives;

namespace AethericForge.Runtime.Models.Storage;

public sealed record StorageReference : IStorageReference
{
    [JsonConstructor]
    public StorageReference(string store, string key)
    {
        Store = NormalizeRequired(store, nameof(store));
        Key = NormalizeRequired(key, nameof(key));
    }

    public string Store { get; }
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
