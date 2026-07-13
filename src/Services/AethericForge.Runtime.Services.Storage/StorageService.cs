using AethericForge.Runtime.Abstractions.Interfaces.Storage;
using AethericForge.Runtime.Abstractions.Interfaces.Storage.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Storage.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Storage.Services;

namespace AethericForge.Runtime.Services.Storage;

public sealed class StorageService : IStorageService
{
    private readonly IReadOnlyDictionary<string, IStorageProvider> _providers;

    public StorageService(IEnumerable<IStorageProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToDictionary(provider => provider.Store, StringComparer.Ordinal);

        if (_providers.Count == 0)
        {
            throw new ArgumentException("At least one storage provider is required.", nameof(providers));
        }
    }

    public Task<IStorageReference> PutAsync(
        string store,
        string key,
        Stream content,
        IStorageMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();

        return GetProvider(store).PutAsync(key, content, metadata, ct);
    }

    public Task<Stream> OpenReadAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).OpenReadAsync(reference, ct);
    }

    public Task<IStorageMetadata?> StatAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).StatAsync(reference, ct);
    }

    public Task<bool> ExistsAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).ExistsAsync(reference, ct);
    }

    public Task<bool> DeleteAsync(
        IStorageReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).DeleteAsync(reference, ct);
    }

    private IStorageProvider GetProvider(IStorageReference reference)
    {
        return GetProvider(reference.Store);
    }

    private IStorageProvider GetProvider(string store)
    {
        if (string.IsNullOrWhiteSpace(store))
        {
            throw new ArgumentException("Store is required.", nameof(store));
        }

        if (_providers.TryGetValue(store.Trim(), out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"No storage provider is registered for store '{store}'.");
    }
}
