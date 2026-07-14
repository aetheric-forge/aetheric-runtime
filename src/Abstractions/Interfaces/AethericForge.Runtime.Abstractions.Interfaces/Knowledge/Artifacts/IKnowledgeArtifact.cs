using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;

public interface IKnowledgeArtifact : IKnowledgeObject
{
    IKnowledgeAuthority? Authority { get; }
    IReadOnlyCollection<IKnowledgeRepresentation> Representations { get; }
    IReadOnlyCollection<IKnowledgeReference> Lineage { get; }
}
