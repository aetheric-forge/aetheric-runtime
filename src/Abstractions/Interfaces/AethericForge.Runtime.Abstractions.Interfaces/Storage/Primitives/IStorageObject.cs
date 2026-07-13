namespace AethericForge.Runtime.Abstractions.Interfaces.Storage.Primitives;

public interface IStorageObject
{
    IStorageReference Reference { get; }
    IStorageMetadata Metadata { get; }
}
