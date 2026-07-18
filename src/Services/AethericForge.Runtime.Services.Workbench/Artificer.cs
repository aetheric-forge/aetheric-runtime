using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;

namespace AethericForge.Runtime.Services.Workbench;

public sealed class Artificer(IStagingService stagingService, ITeam<IWorkbenchWorker> team) : IArtificer
{
    private readonly IStagingService _stagingService = stagingService ?? throw new ArgumentNullException(nameof(stagingService));
    public ITeam<IWorkbenchWorker> Team { get; } = team ?? throw new ArgumentNullException(nameof(team));

    public Task<IStagingMetadata?> StatAsync(IStagingReference reference, CancellationToken ct = default)
        => _stagingService.StatAsync(reference, ct);

    public Task<bool> ExistsAsync(IStagingReference reference, CancellationToken ct = default)
        => _stagingService.ExistsAsync(reference, ct);

    public Task<bool> DeleteAsync(IStagingReference reference, CancellationToken ct = default)
        => _stagingService.DeleteAsync(reference, ct);

    public Task PinAsync(IStagingReference reference, CancellationToken ct = default)
        => _stagingService.PinAsync(reference, ct);

    public Task UnpinAsync(IStagingReference reference, CancellationToken ct = default)
        => _stagingService.UnpinAsync(reference, ct);

    public Task<IStagingLock> AcquireLockAsync(IStagingReference reference, TimeSpan? timeout = null, CancellationToken ct = default)
        => _stagingService.AcquireLockAsync(reference, timeout, ct);
}
