namespace AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;

public interface IArchiveObject
{
    IArchiveReference Reference { get; }
    IArchiveMetadata Metadata { get; }
}
