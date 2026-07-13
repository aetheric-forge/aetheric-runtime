using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Services;

namespace AethericForge.Runtime.Services.Staging;

public sealed class StagingService : IStagingService
{
    private readonly IReadOnlyDictionary<string, IStagingProvider> _providers;

    public StagingService(IEnumerable<IStagingProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToDictionary(provider => provider.Stage, StringComparer.Ordinal);

        if (_providers.Count == 0)
        {
            throw new ArgumentException("At least one staging provider is required.", nameof(providers));
        }
    }

    public Task<IStagingReference> PutAsync(
        string stage,
        string key,
        Stream content,
        IStagingMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();

        return GetProvider(stage).PutAsync(key, content, metadata, ct);
    }

    public Task<Stream> OpenReadAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).OpenReadAsync(reference, ct);
    }

    public Task<IStagingMetadata?> StatAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).StatAsync(reference, ct);
    }

    public Task<bool> ExistsAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).ExistsAsync(reference, ct);
    }

    public Task<bool> DeleteAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).DeleteAsync(reference, ct);
    }

    public Task<IStagingObject?> GetAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).GetAsync(reference, ct);
    }

    public Task PinAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).PinAsync(reference, ct);
    }

    public Task UnpinAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).UnpinAsync(reference, ct);
    }

    public Task<IStagingLock> AcquireLockAsync(
        IStagingReference reference,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();

        return GetProvider(reference).AcquireLockAsync(reference, timeout, ct);
    }

    private IStagingProvider GetProvider(IStagingReference reference)
    {
        return GetProvider(reference.Stage);
    }

    private IStagingProvider GetProvider(string stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            throw new ArgumentException("Stage is required.", nameof(stage));
        }

        if (_providers.TryGetValue(stage.Trim(), out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"No staging provider is registered for stage '{stage}'.");
    }
}
