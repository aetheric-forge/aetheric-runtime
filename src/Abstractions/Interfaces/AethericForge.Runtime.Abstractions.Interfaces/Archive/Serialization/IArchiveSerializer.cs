namespace AethericForge.Runtime.Abstractions.Interfaces.Archive.Serialization;

public interface IArchiveSerializer
{
    string ContentType { get; }

    ValueTask SerializeAsync<T>(
        Stream destination,
        T value,
        CancellationToken ct = default);

    ValueTask<T?> DeserializeAsync<T>(
        Stream source,
        CancellationToken ct = default);
}
