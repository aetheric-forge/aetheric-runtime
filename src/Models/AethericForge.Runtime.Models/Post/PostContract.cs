using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

namespace AethericForge.Runtime.Models.Post;

public sealed record PostContract : IPostContract
{
    public PostContract(
        string name,
        string version,
        PostIntent intent)
    {
        Name = NormalizeRequired(name, nameof(name));
        Version = NormalizeRequired(version, nameof(version));
        Intent = intent;
    }

    public bool Equals(PostContract? other) =>
        other is not null &&
        StringComparer.Ordinal.Equals(Name, other.Name) &&
        StringComparer.Ordinal.Equals(Version, other.Version) &&
        Intent == other.Intent;

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Version, (int)Intent);
    }

    public string Name { get; }
    
    public string Version { get; }
    public PostIntent Intent { get; }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
