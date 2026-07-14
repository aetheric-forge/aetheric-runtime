using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Knowledge.Primitives;

namespace AethericForge.Runtime.Models.Knowledge.Artifacts;

public class KnowledgeArtifact : KnowledgeObjectBase, IKnowledgeArtifact
{
    public KnowledgeArtifact(
        IKnowledgeReference reference,
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        KnowledgeLifecycle lifecycle = KnowledgeLifecycle.Catalogued,
        KnowledgeState state = KnowledgeState.Available,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
        : base(
            reference, 
            descriptor, 
            lifecycle, 
            state, 
            createdAtUtc ?? DateTimeOffset.UtcNow, 
            updatedAtUtc ?? createdAtUtc ?? DateTimeOffset.UtcNow)
    {
        Representations = representations?.ToArray() ?? throw new ArgumentNullException(nameof(representations));
        Lineage = lineage?.ToArray() ?? [];
    }

    public IReadOnlyCollection<IKnowledgeRepresentation> Representations { get; }
    public IReadOnlyCollection<IKnowledgeReference> Lineage { get; }
}
