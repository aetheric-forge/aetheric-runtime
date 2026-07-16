using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Library;

public sealed class Library(
    ILibraryContext context,
    ILibraryService vault,
    ILibrarian librarian)
    : InstitutionBase(context), ILibrary
{
    public new ILibraryContext Context { get; } = context;

    public ILibrarian Librarian { get; } = librarian ?? throw new ArgumentNullException(nameof(librarian));
    private readonly ILibraryService _vault = vault ?? throw new ArgumentNullException(nameof(vault));

    public Task<IKnowledgeArtifact?> GetArtifactAsync(
        IKnowledgeReference reference,
        CancellationToken ct = default)
        => Librarian.GetArtifactAsync(reference, ct);

    public Task<IKnowledgeArtifact> PublishArtifactAsync(
        IKnowledgeDescriptor descriptor,
        IEnumerable<IKnowledgeRepresentation> representations,
        IEnumerable<IKnowledgeReference>? lineage = null,
        IKnowledgeAuthority? authority = null,
        CancellationToken ct = default)
        => Librarian.PublishArtifactAsync(descriptor, representations, lineage, authority, ct);
}
