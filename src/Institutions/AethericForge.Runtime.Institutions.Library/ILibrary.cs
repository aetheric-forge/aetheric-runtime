using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;

namespace AethericForge.Runtime.Institutions.Library;

/// <summary>
/// Represents an Institution that serves as a repository of knowledge.
/// </summary>
public interface ILibrary : IInstitution
{
    /// <summary>
    /// Gets a knowledge artifact by its reference.
    /// </summary>
    /// <param name="reference">The reference to the artifact.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The artifact, or null if not found.</returns>
    Task<IKnowledgeArtifact?> GetArtifactAsync(
        IKnowledgeReference reference,
        CancellationToken ct = default);

    /// <summary>
    /// Publishes a new knowledge artifact.
    /// </summary>
    /// <param name="descriptor">The descriptor for the artifact.</param>
    /// <param name="representations">The representations of the artifact.</param>
    /// <param name="lineage">Optional lineage of the artifact.</param>
    /// <param name="authority">Optional authority for the artifact.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The published artifact.</returns>
    Task<IKnowledgeArtifact> PublishArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        IKnowledgeAuthority? authority = null,
        CancellationToken ct = default);
}
