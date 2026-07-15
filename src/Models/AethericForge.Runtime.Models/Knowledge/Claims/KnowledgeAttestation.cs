using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;

namespace AethericForge.Runtime.Models.Knowledge.Claims;

public sealed class KnowledgeAttestation : KnowledgeClaim, IKnowledgeAttestation
{
    public KnowledgeAttestation(
        IKnowledgeReference reference,
        IKnowledgeDescriptor descriptor,
        IIdentitySubject asserter,
        string claimType,
        IKnowledgeObject subject,
        byte[] signature,
        string algorithm,
        string? keyId = null,
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
            asserter, 
            claimType, 
            subject, 
            representations, 
            statement, 
            lineage, 
            lifecycle, 
            state, 
            createdAtUtc, 
            updatedAtUtc)
    {
        Signature = signature ?? throw new ArgumentNullException(nameof(signature));
        Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
        KeyId = keyId;
    }

    public byte[] Signature { get; }
    public string Algorithm { get; }
    public string? KeyId { get; }
}
