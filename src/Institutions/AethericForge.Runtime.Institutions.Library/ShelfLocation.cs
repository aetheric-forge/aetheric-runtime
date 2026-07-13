using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Institutions.Library;

public sealed record ShelfLocation(
    string ShelfName,
    IKnowledgeReference KnowledgeReference,
    IArchiveReference ArchiveReference);
