using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Providers;
using AethericForge.Runtime.Models.Staging;

namespace AethericForge.Runtime.Providers.Staging.InMemory;

public sealed class InMemoryStagingProvider : IStagingProvider
{
    private readonly object _sync = new();
    private readonly Dictionary<string, (byte[] Content, IStagingMetadata Metadata)> _storage = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pins = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public InMemoryStagingProvider(string stage)
    {
        Stage = NormalizeRequired(stage, nameof(stage));
    }

    public string Stage { get; }

    public async Task<IStagingReference> PutAsync(
        string key,
        Stream content,
        IStagingMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();

        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, ct);
        var bytes = memoryStream.ToArray();

        var finalMetadata = new StagingMetadata(
            contentType: metadata?.ContentType,
            contentLength: bytes.Length,
            eTag: metadata?.ETag ?? Guid.NewGuid().ToString("N"),
            lastModifiedUtc: metadata?.LastModifiedUtc ?? DateTimeOffset.UtcNow,
            attributes: metadata?.Attributes);

        lock (_sync)
        {
            _storage[key] = (bytes, finalMetadata);
        }

        return new StagingReference(Stage, key);
    }

    public Task<Stream> OpenReadAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_storage.TryGetValue(reference.Key, out var entry))
            {
                return Task.FromResult<Stream>(new MemoryStream(entry.Content));
            }
        }

        throw new KeyNotFoundException($"Staging key '{reference.Key}' not found in stage '{Stage}'.");
    }

    public Task<IStagingMetadata?> StatAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            return Task.FromResult(_storage.TryGetValue(reference.Key, out var entry) ? entry.Metadata : null);
        }
    }

    public Task<bool> ExistsAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        
        if (!string.Equals(Stage, reference.Stage, StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            return Task.FromResult(_storage.ContainsKey(reference.Key));
        }
    }

    public Task<bool> DeleteAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_pins.Contains(reference.Key))
            {
                return Task.FromResult(false);
            }
            return Task.FromResult(_storage.Remove(reference.Key));
        }
    }

    public Task<IStagingObject?> GetAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            return Task.FromResult<IStagingObject?>(
                _storage.TryGetValue(reference.Key, out var entry)
                    ? new StagingObject(reference, entry.Metadata)
                    : null);
        }
    }

    public Task PinAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _pins.Add(reference.Key);
        }

        return Task.CompletedTask;
    }

    public Task UnpinAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _pins.Remove(reference.Key);
        }

        return Task.CompletedTask;
    }

    public async Task<IStagingLock> AcquireLockAsync(
        IStagingReference reference,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        SemaphoreSlim semaphore;
        lock (_sync)
        {
            if (!_locks.TryGetValue(reference.Key, out semaphore!))
            {
                semaphore = new SemaphoreSlim(1, 1);
                _locks[reference.Key] = semaphore;
            }
        }

        var acquired = timeout.HasValue
            ? await semaphore.WaitAsync(timeout.Value, ct)
            : await semaphore.WaitAsync(-1, ct);

        return new InMemoryStagingLock(this, reference, semaphore, acquired);
    }

    private void ReleaseLock(IStagingReference reference, SemaphoreSlim semaphore)
    {
        semaphore.Release();
    }

    private void EnsureOwns(IStagingReference reference)
    {
        if (!string.Equals(Stage, reference.Stage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Provider stage '{Stage}' cannot handle reference for stage '{reference.Stage}'.");
        }
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }

    private sealed class InMemoryStagingLock : IStagingLock
    {
        private readonly InMemoryStagingProvider _provider;
        private readonly SemaphoreSlim _semaphore;
        private int _disposed;

        public InMemoryStagingLock(
            InMemoryStagingProvider provider,
            IStagingReference reference,
            SemaphoreSlim semaphore,
            bool acquired)
        {
            _provider = provider;
            Reference = reference;
            _semaphore = semaphore;
            IsAcquired = acquired;
        }

        public IStagingReference Reference { get; }
        public bool IsAcquired { get; }

        public async Task ReleaseAsync(CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                if (IsAcquired)
                {
                    _provider.ReleaseLock(Reference, _semaphore);
                }
            }
            await Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
        }
    }
}
