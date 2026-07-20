using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Models.Institutions;

namespace AethericForge.Runtime.Institutions.Library;

public sealed class Library(
    ILibraryContext context,
    ILibrarian librarian,
    ICurator curator)
    : InstitutionBase(context), ILibrary
{
    public new ILibraryContext Context { get; } = context;

    public ILibrarian Librarian { get; } = librarian ?? throw new ArgumentNullException(nameof(librarian));
    
    public ICurator Curator { get; } = curator ?? throw new ArgumentNullException(nameof(curator));
}
