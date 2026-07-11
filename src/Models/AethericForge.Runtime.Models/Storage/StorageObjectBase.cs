using AethericForge.Runtime.Abstractions.Interfaces.Storage;

namespace AethericForge.Runtime.Models.Storage;

public abstract class StorageObjectBase : IStorageObject
{
    protected StorageObjectBase(
        IStorageReference reference,
        IStorageMetadata? metadata = null)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        Metadata = metadata ?? new StorageMetadata();
    }

    public IStorageReference Reference { get; }
    public IStorageMetadata Metadata { get; }
}
