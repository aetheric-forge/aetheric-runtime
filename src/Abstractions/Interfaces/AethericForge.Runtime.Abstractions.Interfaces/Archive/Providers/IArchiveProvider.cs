using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;

public interface IArchiveProvider : IArchiveVault
{
    string Store { get; }

    Task<IArchiveReference> PutAsync(
        string key,
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default);

    Task<IArchiveReference> IArchiveVault.ArchiveAsync(
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default) => PutAsync(Guid.NewGuid().ToString(), content, metadata, ct);

    Task<Stream> IArchiveVault.RetrieveAsync(
        IArchiveReference reference,
        CancellationToken ct = default) => RetrieveAsync(reference, ct);

    Task<Stream> RetrieveAsync(
        IArchiveReference reference,
        CancellationToken ct = default);

    Task<IArchiveMetadata?> StatAsync(
        IArchiveReference reference,
        CancellationToken ct = default);

    Task<bool> ExistsAsync(
        IArchiveReference reference,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        IArchiveReference reference,
        CancellationToken ct = default);
}
