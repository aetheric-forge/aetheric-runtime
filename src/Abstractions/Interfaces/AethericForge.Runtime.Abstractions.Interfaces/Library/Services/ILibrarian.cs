using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;

namespace AethericForge.Runtime.Abstractions.Interfaces.Library.Services;

/// <summary>
/// Provides high-level library services for knowledge artifacts.
/// </summary>
public interface ILibrarian : IAuthority<ILibraryClerk>
{
    Task<IKnowledgeArtifact?> GetArtifactAsync(
        IKnowledgeReference reference,
        CancellationToken ct = default);

    Task<IKnowledgeArtifact> PublishArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        IKnowledgeAuthority? authority = null,
        CancellationToken ct = default);
}
