using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;

namespace AethericForge.Runtime.Services.Library;

public sealed class Librarian(IKnowledgeService knowledgeService, ITeam<ILibraryClerk> team) : ILibrarian
{
    public Task<IKnowledgeArtifact?> GetArtifactAsync(
        IKnowledgeReference reference,
        CancellationToken ct = default)
        => knowledgeService.GetArtifactAsync(reference, ct);

    public Task<IReadOnlyCollection<IKnowledgeArtifact>> FindArtifactsAsync(
        IKnowledgeAuthority authority,
        CancellationToken ct = default)
        => knowledgeService.FindArtifactsAsync(authority, ct);

    public Task<IKnowledgeArtifact> PublishArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        IKnowledgeAuthority? authority = null,
        CancellationToken ct = default)
        => knowledgeService.PublishArtifactAsync(descriptor, representations, lineage, authority, ct);

    public ITeam<ILibraryClerk> Team { get; } = team;
}
