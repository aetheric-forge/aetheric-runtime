using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;
using System.Collections.ObjectModel;

namespace AethericForge.Runtime.Models.Identity.Directory;

public sealed record ExternalIdentity : IExternalIdentity
{
    public ExternalIdentity(
        IExternalIdentityReference reference,
        string? displayName = null,
        bool isEnabled = true,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        DisplayName = DirectoryValue.NormalizeOptional(displayName);
        IsEnabled = isEnabled;
        Properties = NormalizeProperties(properties);
    }

    public IExternalIdentityReference Reference { get; }
    public string? DisplayName { get; }
    public bool IsEnabled { get; }
    public IReadOnlyDictionary<string, string> Properties { get; }

    private static IReadOnlyDictionary<string, string> NormalizeProperties(
        IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties)
        {
            var key = DirectoryValue.NormalizeRequired(property.Key, nameof(properties));
            var value = DirectoryValue.NormalizeRequired(property.Value, nameof(properties));

            if (!normalized.TryAdd(key, value))
            {
                throw new ArgumentException($"Duplicate identity property '{key}'.", nameof(properties));
            }
        }

        return new ReadOnlyDictionary<string, string>(normalized);
    }
}
