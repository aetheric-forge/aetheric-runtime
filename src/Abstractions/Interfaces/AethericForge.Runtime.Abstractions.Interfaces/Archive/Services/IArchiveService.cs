using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;

public interface IArchiveService : IArchiveVault
{
    Task<IArchiveReference> PutAsync(
        string store,
        string key,
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default);

    new Task<Stream> RetrieveAsync(
        IArchiveReference reference,
        CancellationToken ct = default);

    new Task<IArchiveMetadata?> StatAsync(
        IArchiveReference reference,
        CancellationToken ct = default);

    new Task<bool> ExistsAsync(
        IArchiveReference reference,
        CancellationToken ct = default);

    new Task<bool> DeleteAsync(
        IArchiveReference reference,
        CancellationToken ct = default);
}
