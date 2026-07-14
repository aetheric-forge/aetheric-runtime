using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;

public interface IKnowledgeService
{
    Task<IKnowledgeArtifact?> GetArtifactAsync(IKnowledgeReference reference, CancellationToken cancellationToken = default);
    
    Task<IKnowledgeArtifact> PublishArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        CancellationToken cancellationToken = default);

    Task<IKnowledgeArtifact?> ResolveReferenceAsync(
        IKnowledgeReference reference,
        CancellationToken cancellationToken = default);
}
