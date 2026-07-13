using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Storage;
using AethericForge.Runtime.Abstractions.Interfaces.Storage.Primitives;

namespace AethericForge.Runtime.Institutions.Library;

public sealed record ShelfLocation(
    string ShelfName,
    IKnowledgeReference KnowledgeReference,
    IStorageReference StorageReference);
