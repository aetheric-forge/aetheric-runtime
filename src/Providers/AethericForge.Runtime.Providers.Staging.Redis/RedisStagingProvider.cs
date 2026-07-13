using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Providers;
using AethericForge.Runtime.Models.Staging;
using StackExchange.Redis;
using System.Text.Json;

namespace AethericForge.Runtime.Providers.Staging.Redis;

public sealed class RedisStagingProvider : IStagingProvider
{
    private readonly IDatabase _db;
    private readonly string _stage;

    public RedisStagingProvider(IConnectionMultiplexer redis, string stage)
    {
        ArgumentNullException.ThrowIfNull(redis);
        _db = redis.GetDatabase();
        _stage = string.IsNullOrWhiteSpace(stage) ? throw new ArgumentException("Stage is required.", nameof(stage)) : stage.Trim();
    }

    public string Stage => _stage;

    public async Task<IStagingReference> PutAsync(
        string key,
        Stream content,
        IStagingMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ct.ThrowIfCancellationRequested();

        var redisKey = GetRedisKey(key);

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var data = ms.ToArray();

        var finalMetadata = new StagingMetadata(
            contentType: metadata?.ContentType,
            contentLength: metadata?.ContentLength ?? data.Length,
            eTag: metadata?.ETag,
            lastModifiedUtc: metadata?.LastModifiedUtc ?? DateTimeOffset.UtcNow,
            expiration: metadata?.Expiration,
            attributes: metadata?.Attributes
        );

        await _db.HashSetAsync(redisKey, new HashEntry[]
        {
            new("content", data),
            new("metadata", JsonSerializer.Serialize(finalMetadata))
        });

        if (finalMetadata.Expiration != null)
        {
            await _db.KeyExpireAsync(redisKey, finalMetadata.Expiration.Value);
        }

        return new StagingReference(_stage, key);
    }

    public async Task<Stream> OpenReadAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();
        EnsureOwns(reference);

        var redisKey = GetRedisKey(reference.Key);
        var data = await _db.HashGetAsync(redisKey, "content");

        if (data.IsNull)
        {
            throw new KeyNotFoundException($"Key '{reference.Key}' not found in stage '{_stage}'.");
        }

        return new MemoryStream(data!);
    }

    public async Task<IStagingMetadata?> StatAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();
        EnsureOwns(reference);

        var redisKey = GetRedisKey(reference.Key);
        var metadataJson = await _db.HashGetAsync(redisKey, "metadata");

        if (metadataJson.IsNull)
        {
            return null;
        }

        return JsonSerializer.Deserialize<StagingMetadata>((string)metadataJson!);
    }

    public async Task<bool> ExistsAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();
        EnsureOwns(reference);

        return await _db.KeyExistsAsync(GetRedisKey(reference.Key));
    }

    public async Task<bool> DeleteAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();
        EnsureOwns(reference);

        return await _db.KeyDeleteAsync(GetRedisKey(reference.Key));
    }

    public async Task<IStagingObject?> GetAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();
        EnsureOwns(reference);

        var redisKey = GetRedisKey(reference.Key);
        var entries = await _db.HashGetAllAsync(redisKey);
        
        if (entries.Length == 0)
        {
            return null;
        }

        var dict = entries.ToDictionary();
        var metadataJson = dict.GetValueOrDefault("metadata");
        var metadata = metadataJson.IsNull 
            ? new StagingMetadata() 
            : JsonSerializer.Deserialize<StagingMetadata>((string)metadataJson!);

        return new StagingObject(reference, metadata);
    }

    public async Task PinAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();
        EnsureOwns(reference);

        // Pinning in Redis means removing the expiration
        await _db.KeyPersistAsync(GetRedisKey(reference.Key));
    }

    public async Task UnpinAsync(
        IStagingReference reference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();
        EnsureOwns(reference);

        // Unpinning means restoring an expiration.
        // We'll try to restore the original expiration from metadata if available.
        var metadata = await StatAsync(reference, ct);
        var expiration = metadata?.Expiration ?? TimeSpan.FromHours(24);

        await _db.KeyExpireAsync(GetRedisKey(reference.Key), expiration);
    }

    public async Task<IStagingLock> AcquireLockAsync(
        IStagingReference reference,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ct.ThrowIfCancellationRequested();
        EnsureOwns(reference);

        var lockKey = GetLockKey(reference.Key);
        var lockToken = Guid.NewGuid().ToString();
        var acquired = await _db.LockTakeAsync(lockKey, lockToken, timeout ?? TimeSpan.FromMinutes(1));
        
        return new RedisStagingLock(reference, _db, lockKey, lockToken, acquired);
    }

    private void EnsureOwns(IStagingReference reference)
    {
        if (!string.Equals(_stage, reference.Stage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Provider stage '{_stage}' cannot handle reference for stage '{reference.Stage}'.");
        }
    }

    private RedisKey GetRedisKey(string key) => $"{_stage}:data:{key}";
    private RedisKey GetLockKey(string key) => $"{_stage}:lock:{key}";
}
