using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Models.Knowledge.Primitives;

public sealed record KnowledgeReference : IKnowledgeReference
{
    public KnowledgeReference(
        string set,
        string kind,
        string name,
        string version,
        int revision = 0,
        string? contentHash = null)
    {
        Set = set ?? throw new ArgumentNullException(nameof(set));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Revision = revision;
        ContentHash = contentHash;
    }

    public string Set { get; init; }
    public string Kind { get; init; }
    public string Name { get; init; }
    public string Version { get; init; }
    public int Revision { get; init; }
    public string? ContentHash { get; init; }

    public override string ToString() => $"{Set}:{Kind}/{Name}@{Version}.{Revision}";
}
