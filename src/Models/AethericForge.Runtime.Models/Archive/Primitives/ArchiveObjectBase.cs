using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;

namespace AethericForge.Runtime.Models.Archive.Primitives;

public abstract class ArchiveObjectBase : IArchiveObject
{
    protected ArchiveObjectBase(
        IArchiveReference reference,
        IArchiveMetadata? metadata = null)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        Metadata = metadata ?? new ArchiveMetadata();
    }

    public IArchiveReference Reference { get; }
    public IArchiveMetadata Metadata { get; }
}
