using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Relationships;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Knowledge.Artifacts;

namespace AethericForge.Runtime.Models.Knowledge.Relationships;

public sealed class KnowledgeRelationship : KnowledgeArtifact, IKnowledgeRelationship
{
    public KnowledgeRelationship(
        IKnowledgeReference reference,
        IKnowledgeDescriptor descriptor,
        string relationshipType,
        IEnumerable<IKnowledgeReference> participants,
        IEnumerable<IKnowledgeRepresentation>? representations = null,
        IEnumerable<IKnowledgeReference>? lineage = null,
        KnowledgeLifecycle lifecycle = KnowledgeLifecycle.Catalogued,
        KnowledgeState state = KnowledgeState.Available,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null,
        IKnowledgeAuthority? authority = null)
        : base(
            reference, 
            descriptor, 
            representations ?? [], 
            lineage, 
            lifecycle, 
            state, 
            createdAtUtc, 
            updatedAtUtc,
            authority)
    {
        RelationshipType = relationshipType ?? throw new ArgumentNullException(nameof(relationshipType));
        Participants = participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants));
    }

    public string RelationshipType { get; }
    public IReadOnlyCollection<IKnowledgeReference> Participants { get; }
}
