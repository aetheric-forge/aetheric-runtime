using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Models.Archive.Primitives;

namespace AethericForge.Runtime.Providers.Archive.InMemory;

public sealed class InMemoryArchiveProvider : IArchiveProvider
{
    private readonly object _sync = new();
    private readonly Dictionary<string, (byte[] Content, IArchiveMetadata Metadata)> _storage = new(StringComparer.Ordinal);

    public InMemoryArchiveProvider(string store)
    {
        Store = NormalizeRequired(store, nameof(store));
    }

    public string Store { get; }

    public async Task<IArchiveReference> PutAsync(
        string key,
        Stream content,
        IArchiveMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();

        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, ct);
        var bytes = memoryStream.ToArray();

        var finalMetadata = new ArchiveMetadata(
            contentType: metadata?.ContentType,
            contentLength: bytes.Length,
            eTag: metadata?.ETag ?? Guid.NewGuid().ToString("N"),
            lastModifiedUtc: metadata?.LastModifiedUtc ?? DateTimeOffset.UtcNow,
            attributes: metadata?.Attributes);

        lock (_sync)
        {
            _storage[key] = (bytes, finalMetadata);
        }

        return new ArchiveReference(Store, key);
    }

    public Task<Stream> RetrieveAsync(
        IArchiveReference reference,
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

        throw new KeyNotFoundException($"Archive key '{reference.Key}' not found in store '{Store}'.");
    }

    public Task<IArchiveMetadata?> StatAsync(
        IArchiveReference reference,
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
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            return Task.FromResult(_storage.ContainsKey(reference.Key));
        }
    }

    public Task<bool> DeleteAsync(
        IArchiveReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        EnsureOwns(reference);
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            return Task.FromResult(_storage.Remove(reference.Key));
        }
    }

    private void EnsureOwns(IArchiveReference reference)
    {
        if (!string.Equals(Store, reference.Store, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Provider store '{Store}' cannot handle reference for store '{reference.Store}'.");
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
}
