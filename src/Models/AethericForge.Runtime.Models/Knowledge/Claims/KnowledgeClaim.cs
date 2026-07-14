using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Knowledge.Artifacts;

namespace AethericForge.Runtime.Models.Knowledge.Claims;

public class KnowledgeClaim : KnowledgeArtifact, IKnowledgeClaim
{
    public KnowledgeClaim(
        IKnowledgeReference reference,
        IKnowledgeDescriptor descriptor,
        IIdentitySubject asserter,
        string claimType,
        IKnowledgeObject subject,
        IEnumerable<IKnowledgeRepresentation>? representations = null,
        object? statement = null,
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
        ClaimType = claimType ?? throw new ArgumentNullException(nameof(claimType));
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        Statement = statement;
    }

    public IIdentitySubject Asserter { get; }
    public string ClaimType { get; }
    public IKnowledgeObject Subject { get; }
    public object? Statement { get; }
}
