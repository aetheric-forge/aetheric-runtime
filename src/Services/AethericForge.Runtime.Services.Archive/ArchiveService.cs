using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;

namespace AethericForge.Runtime.Services.Archive;

public sealed class ArchiveService : IArchiveService
{
    private readonly IReadOnlyDictionary<string, IArchiveProvider> _providers;

    public ArchiveService(IEnumerable<IArchiveProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToDictionary(provider => provider.Store, StringComparer.Ordinal);

        if (_providers.Count == 0)
        {
            throw new ArgumentException("At least one archive provider is required.", nameof(providers));
        }
    }

    public Task<IArchiveReference> PutAsync(
        string store,
        string key,
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();

        return GetProvider(store).PutAsync(key, content, metadata, ct);
    }

    public Task<IArchiveReference> ArchiveAsync(
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();

        return _providers.Values.First().ArchiveAsync(content, metadata, ct);
    }

    public Task<Stream> RetrieveAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).RetrieveAsync(reference, ct);
    }

    public Task<IArchiveMetadata?> StatAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).StatAsync(reference, ct);
    }

    public Task<bool> ExistsAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).ExistsAsync(reference, ct);
    }

    public Task<bool> DeleteAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).DeleteAsync(reference, ct);
    }

    private IArchiveProvider GetProvider(IArchiveReference reference)
    {
        return GetProvider(reference.Store);
    }

    private IArchiveProvider GetProvider(string store)
    {
        if (string.IsNullOrWhiteSpace(store))
        {
            throw new ArgumentException("Store is required.", nameof(store));
        }

        if (_providers.TryGetValue(store.Trim(), out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"No archive provider is registered for store '{store}'.");
    }
}
