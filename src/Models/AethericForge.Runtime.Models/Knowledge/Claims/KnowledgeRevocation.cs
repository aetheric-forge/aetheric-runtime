using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Knowledge.Artifacts;

namespace AethericForge.Runtime.Models.Knowledge.Claims;

public sealed class KnowledgeRevocation : KnowledgeArtifact, IKnowledgeRevocation
{
    public KnowledgeRevocation(
        IKnowledgeReference reference,
        IKnowledgeDescriptor descriptor,
        IIdentitySubject asserter,
        IKnowledgeReference target,
        string? reason = null,
        IEnumerable<IKnowledgeRepresentation>? representations = null,
        IEnumerable<IKnowledgeReference>? lineage = null,
        KnowledgeLifecycle lifecycle = KnowledgeLifecycle.Catalogued,
        KnowledgeState state = KnowledgeState.Available,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null)
        : base(
            reference, 
            descriptor, 
            representations ?? [], 
            lineage, 
            lifecycle, 
            state, 
            createdAtUtc, 
            updatedAtUtc)
    {
        Asserter = asserter ?? throw new ArgumentNullException(nameof(asserter));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Reason = reason;
    }

    public IIdentitySubject Asserter { get; }
    public IKnowledgeReference Target { get; }
    public string? Reason { get; }
}
