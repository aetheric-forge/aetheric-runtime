using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.References;

namespace AethericForge.Runtime.Models.Knowledge.References;

public sealed record AuthoritativeReference : IAuthoritativeReference
{
    public AuthoritativeReference(
        string scheme,
        string kind,
        string name,
        string version,
        IKnowledgeAuthority authority,
        string role,
        int revision = 0,
        string? contentHash = null,
        string? signature = null)
    {
        Scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Authority = authority ?? throw new ArgumentNullException(nameof(authority));
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Revision = revision;
        ContentHash = contentHash;
        Signature = signature;
    }

    public string Scheme { get; init; }
    public string Kind { get; init; }
    public string Name { get; init; }
    public string Version { get; init; }
    public int Revision { get; init; }
    public string? ContentHash { get; init; }
    public IKnowledgeAuthority Authority { get; init; }
    public string Role { get; init; }
    public string? Signature { get; init; }

    public override string ToString() => $"{Authority.Identity.SubjectId}@{Scheme}:{Kind}/{Name}#{Role}";
}
