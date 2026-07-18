using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.References;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;

public interface IKnowledgeProvider
{
    string Scheme { get; }
    
    Task<IKnowledgeArtifact?> GetArtifactAsync(IKnowledgeReference reference, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IKnowledgeArtifact>> FindArtifactsAsync(
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);
    
    Task<IKnowledgeArtifact> StoreArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        IKnowledgeAuthority? authority = null,
        CancellationToken cancellationToken = default);

    Task SetAuthoritativeReferenceAsync(
        IAuthoritativeReference reference, 
        IKnowledgeReference target, 
        CancellationToken cancellationToken = default);

    Task<IKnowledgeReference?> ResolveAuthoritativeReferenceAsync(
        IAuthoritativeReference reference, 
        CancellationToken cancellationToken = default);
}
