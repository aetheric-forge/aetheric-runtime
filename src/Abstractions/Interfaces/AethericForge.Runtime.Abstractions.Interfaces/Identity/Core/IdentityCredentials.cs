namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Core;

public sealed record IdentityCredentials
{
    public IdentityCredentials(
        IdentityScheme scheme,
        IReadOnlyDictionary<string, string> values)
    {
        Scheme = scheme;
        Values = NormalizeValues(values);
    }

    public IdentityScheme Scheme { get; }
    public IReadOnlyDictionary<string, string> Values { get; }

    private static IReadOnlyDictionary<string, string> NormalizeValues(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in values)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Credential keys are required.", nameof(values));
            }

            normalized[key.Trim()] = value ?? throw new ArgumentException("Credential values cannot be null.", nameof(values));
        }

        return normalized;
    }
}
