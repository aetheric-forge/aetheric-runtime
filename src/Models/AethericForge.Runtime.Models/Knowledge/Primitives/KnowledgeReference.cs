using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Models.Knowledge.Primitives;

public sealed record KnowledgeReference : IKnowledgeReference, IComparable<KnowledgeReference>
{
    private const int MaxSetLength = 128;
    private const int MaxKindLength = 64;
    private const int MaxNameLength = 256;
    private const int MaxVersionLength = 64;

    private static readonly Regex SlugPattern =
        new("^[a-z0-9][a-z0-9._-]*[a-z0-9]$|^[a-z0-9]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SemVerPattern =
        new(
            "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[A-Za-z-][0-9A-Za-z-]*)(?:\\.(?:0|[1-9][0-9]*|[A-Za-z-][0-9A-Za-z-]*))*))?(?:\\+([0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Sha256Pattern =
        new("^[a-f0-9]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [JsonConstructor]
    public KnowledgeReference(
        string set,
        string kind,
        string name,
        string version,
        int revision,
        string? contentHash)
    {
        Set = ValidateSlug(set, nameof(set), MaxSetLength);
        Kind = ValidateSlug(kind, nameof(kind), MaxKindLength);
        Name = ValidateSlug(name, nameof(name), MaxNameLength);
        Version = ValidateVersion(version);
        Revision = ValidateRevision(revision);
        ContentHash = NormalizeContentHash(contentHash);
    }

    public KnowledgeReference(
        string set,
        string kind,
        string name,
        string version,
        int revision)
        : this(set, kind, name, version, revision, contentHash: null)
    {
    }

    public string Set { get; }
    public string Kind { get; }
    public string Name { get; }
    public string Version { get; }
    public int Revision { get; }
    public string? ContentHash { get; }

    public int CompareTo(KnowledgeReference? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = StringComparer.Ordinal.Compare(Set, other.Set);
        if (result != 0)
        {
            return result;
        }

        result = StringComparer.Ordinal.Compare(Kind, other.Kind);
        if (result != 0)
        {
            return result;
        }

        result = StringComparer.Ordinal.Compare(Name, other.Name);
        if (result != 0)
        {
            return result;
        }

        result = CompareVersions(Version, other.Version);
        if (result != 0)
        {
            return result;
        }

        return Revision.CompareTo(other.Revision);
    }

    private static string ValidateSlug(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                normalized,
                $"Value must be {maxLength} characters or fewer.");
        }

        if (!SlugPattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                "Value must use lowercase letters, digits, dots, underscores, or hyphens, and must start and end with a letter or digit.",
                parameterName);
        }

        return normalized;
    }

    private static string ValidateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version is required.", nameof(version));
        }

        var normalized = version.Trim();

        if (normalized.Length > MaxVersionLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                normalized,
                $"Version must be {MaxVersionLength} characters or fewer.");
        }

        if (!SemVerPattern.IsMatch(normalized))
        {
            throw new ArgumentException("Version must be a valid semantic version.", nameof(version));
        }

        return normalized;
    }

    private static int ValidateRevision(int revision)
    {
        if (revision < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision),
                revision,
                "Revision must be greater than or equal to 1.");
        }

        return revision;
    }

    private static string? NormalizeContentHash(string? contentHash)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            return null;
        }

        var normalized = contentHash.Trim();

        if (!Sha256Pattern.IsMatch(normalized))
        {
            throw new ArgumentException("ContentHash must be a lowercase SHA-256 hex digest.", nameof(contentHash));
        }

        return normalized;
    }

    private static int CompareVersions(string left, string right)
    {
        var leftVersion = global::System.Version.Parse(left.Split('-', '+')[0]);
        var rightVersion = global::System.Version.Parse(right.Split('-', '+')[0]);

        return leftVersion.CompareTo(rightVersion);
    }
}
