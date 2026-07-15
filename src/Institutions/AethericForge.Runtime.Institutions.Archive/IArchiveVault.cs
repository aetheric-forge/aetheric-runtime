using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;

namespace AethericForge.Runtime.Institutions.Archive;

/// <summary>
/// Provides the operational storage used by an Archive.
/// </summary>
public interface IArchiveVault
{
    Task<IArchiveReference> ArchiveAsync(
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default);

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
