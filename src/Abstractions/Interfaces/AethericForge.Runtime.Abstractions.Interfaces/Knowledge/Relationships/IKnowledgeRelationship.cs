using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Relationships;

public interface IKnowledgeRelationship : IKnowledgeArtifact
{
    string RelationshipType { get; }
    IReadOnlyCollection<IKnowledgeReference> Participants { get; }
}
