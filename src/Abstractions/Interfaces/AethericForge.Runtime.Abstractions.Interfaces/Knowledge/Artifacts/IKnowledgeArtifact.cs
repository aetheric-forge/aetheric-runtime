using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;

public interface IKnowledgeArtifact : IKnowledgeObject
{
    IReadOnlyCollection<IKnowledgeRepresentation> Representations { get; }
    IReadOnlyCollection<IKnowledgeReference> Lineage { get; }
}
