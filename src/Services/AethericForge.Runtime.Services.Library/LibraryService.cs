using System.IO;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;

namespace AethericForge.Runtime.Services.Library;

public sealed class LibraryService(IArchiveVault vault) : ILibraryService
{
    private readonly IArchiveVault _vault = vault;

    public Task<IArchiveReference> ArchiveAsync(
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default)
        => _vault.ArchiveAsync(content, metadata, ct);

    public Task<Stream> RetrieveAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
        => _vault.RetrieveAsync(reference, ct);

    public Task<IArchiveMetadata?> StatAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
        => _vault.StatAsync(reference, ct);

    public Task<bool> ExistsAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
        => _vault.ExistsAsync(reference, ct);

    public Task<bool> DeleteAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
        => _vault.DeleteAsync(reference, ct);
}
