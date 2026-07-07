using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Core;
using AethericForge.Runtime.Abstractions.Interfaces.Storage;

namespace AethericForge.Runtime.Institutions.Library;

public sealed record ShelfLocation(
    string ShelfName,
    IKnowledgeReference KnowledgeReference,
    IStorageReference StorageReference);
