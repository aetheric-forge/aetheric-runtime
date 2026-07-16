using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;

namespace AethericForge.Runtime.Services.Archive;

public sealed class ArchiveVault : IArchiveVault
{
    private readonly IArchiveProvider _provider;

    public ArchiveVault(IArchiveProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public Task<IArchiveReference> ArchiveAsync(Stream content, IArchiveMetadata? metadata = null, CancellationToken ct = default) => _provider.ArchiveAsync(content, metadata, ct);
    public Task<Stream> RetrieveAsync(IArchiveReference reference, CancellationToken ct = default) => _provider.RetrieveAsync(reference, ct);
    public Task<IArchiveMetadata?> StatAsync(IArchiveReference reference, CancellationToken ct = default) => _provider.StatAsync(reference, ct);
    public Task<bool> ExistsAsync(IArchiveReference reference, CancellationToken ct = default) => _provider.ExistsAsync(reference, ct);
    public Task<bool> DeleteAsync(IArchiveReference reference, CancellationToken ct = default) => _provider.DeleteAsync(reference, ct);
}
