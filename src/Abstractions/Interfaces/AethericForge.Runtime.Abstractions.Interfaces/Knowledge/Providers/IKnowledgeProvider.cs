using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;

public interface IKnowledgeProvider
{
    string Scheme { get; }
    
    Task<IKnowledgeArtifact?> GetArtifactAsync(IKnowledgeReference reference, CancellationToken cancellationToken = default);
    
    Task<IKnowledgeArtifact> StoreArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        CancellationToken cancellationToken = default);
}
