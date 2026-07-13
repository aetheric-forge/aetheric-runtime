using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;

public interface IArchiveService
{
    Task<IArchiveReference> PutAsync(
        string store,
        string key,
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default);

    Task<Stream> OpenReadAsync(
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
