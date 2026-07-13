namespace AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;

public interface IStagingLock : IAsyncDisposable
{
    IStagingReference Reference { get; }
    bool IsAcquired { get; }
    Task ReleaseAsync(CancellationToken ct = default);
}
