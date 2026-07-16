using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Models.Knowledge.Primitives;

public sealed record KnowledgeReference : IKnowledgeReference
{
    public KnowledgeReference(
        string scheme,
        string kind,
        string name,
        string version,
        int revision = 0,
        string? contentHash = null)
    {
        Scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Revision = revision;
        ContentHash = contentHash;
    }

    public string Scheme { get; init; }
    public string Kind { get; init; }
    public string Name { get; init; }
    public string Version { get; init; }
    public int Revision { get; init; }
    public string? ContentHash { get; init; }

    public override string ToString() => $"{Scheme}:{Kind}/{Name}@{Version}.{Revision}";
}
