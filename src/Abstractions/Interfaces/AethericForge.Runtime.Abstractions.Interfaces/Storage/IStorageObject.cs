namespace AethericForge.Runtime.Abstractions.Interfaces.Storage;

public interface IStorageObject
{
    IStorageReference Reference { get; }
    IStorageMetadata Metadata { get; }
}
