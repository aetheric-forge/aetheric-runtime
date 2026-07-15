using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Compositions;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Knowledge.Artifacts;

namespace AethericForge.Runtime.Models.Knowledge.Compositions;

public sealed class KnowledgeComposition : KnowledgeArtifact, IKnowledgeComposition
{
    public KnowledgeComposition(
        IKnowledgeReference reference,
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeConstituent> constituents,
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
        Constituents = constituents?.ToArray() ?? throw new ArgumentNullException(nameof(constituents));
    }

    public IReadOnlyCollection<IKnowledgeConstituent> Constituents { get; }
}
