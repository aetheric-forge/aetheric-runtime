using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Library;

public sealed class Library(ILibraryContext context)
    : InstitutionBase(context), ILibrary
{
    public new ILibraryContext Context { get; } = context;

    public Task<IKnowledgeArtifact?> GetArtifactAsync(
        IKnowledgeReference reference,
        CancellationToken ct = default)
        => Context.Knowledge.GetArtifactAsync(reference, ct);

    public Task<IKnowledgeArtifact> PublishArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        IKnowledgeAuthority? authority = null,
        CancellationToken ct = default)
        => Context.Knowledge.PublishArtifactAsync(
            descriptor,
            representations,
            lineage,
            authority,
            ct);
}
