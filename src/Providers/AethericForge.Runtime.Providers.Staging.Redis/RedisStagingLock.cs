using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;
using StackExchange.Redis;

namespace AethericForge.Runtime.Providers.Staging.Redis;

public sealed class RedisStagingLock : IStagingLock
{
    private readonly IDatabase _db;
    private readonly RedisKey _lockKey;
    private readonly RedisValue _lockToken;
    private bool _disposed;

    public RedisStagingLock(
        IStagingReference reference,
        IDatabase db,
        RedisKey lockKey,
        RedisValue lockToken,
        bool isAcquired)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _lockKey = lockKey;
        _lockToken = lockToken;
        IsAcquired = isAcquired;
    }

    public IStagingReference Reference { get; }
    public bool IsAcquired { get; private set; }

    public async Task ReleaseAsync(CancellationToken ct = default)
    {
        if (!IsAcquired || _disposed)
        {
            return;
        }

        if (await _db.LockReleaseAsync(_lockKey, _lockToken))
        {
            IsAcquired = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await ReleaseAsync();
        _disposed = true;
    }
}
