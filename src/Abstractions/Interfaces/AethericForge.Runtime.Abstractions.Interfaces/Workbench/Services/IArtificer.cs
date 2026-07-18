using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;

public interface IArtificer : IAuthority<IWorkbenchWorker>
{
    Task<IStagingReference> PutAsync(
        string stage,
        string key,
        Stream content,
        IStagingMetadata? metadata = null,
        CancellationToken ct = default);

    Task<IStagingObject?> GetAsync(
        IStagingReference reference,
        CancellationToken ct = default);

    Task<Stream> OpenReadAsync(
        IStagingReference reference,
        CancellationToken ct = default);

    Task<IStagingMetadata?> StatAsync(
        IStagingReference reference,
        CancellationToken ct = default);

    Task<bool> ExistsAsync(
        IStagingReference reference,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        IStagingReference reference,
        CancellationToken ct = default);

    Task PinAsync(
        IStagingReference reference,
        CancellationToken ct = default);

    Task UnpinAsync(
        IStagingReference reference,
        CancellationToken ct = default);

    Task<IStagingLock> AcquireLockAsync(
        IStagingReference reference,
        TimeSpan? timeout = null,
        CancellationToken ct = default);
}
