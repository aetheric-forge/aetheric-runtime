using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

namespace AethericForge.Runtime.Models.Post;

public sealed record PostReference : IPostReference
{
    public PostReference(
        string domain,
        string address,
        IPostContract contract,
        IReadOnlyDictionary<string, string>? qualifiers = null)
    {
        Domain = NormalizeRequired(domain, nameof(domain));
        Address = NormalizeRequired(address, nameof(address));
        Contract = contract ?? throw new ArgumentNullException(nameof(contract));
        Qualifiers = NormalizeQualifiers(qualifiers);
    }

    public string Domain { get; }
    public string Address { get; }
    public IPostContract Contract { get; }
    public IReadOnlyDictionary<string, string> Qualifiers { get; }

    private static Dictionary<string, string> NormalizeQualifiers(
        IReadOnlyDictionary<string, string>? qualifiers)
    {
        if (qualifiers is null || qualifiers.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in qualifiers)
        {
            normalized[NormalizeRequired(key, nameof(qualifiers))] = value;
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
