using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Services;

/// <summary>
/// Consumer-side contract: each app implements its own workers for the job names it defines, and its
/// own small dispatch loop that collects commands via <see cref="ICaretaker.CollectNextAsync"/> and runs
/// the matching worker. The Runtime only supplies the custody/outcome mechanism, not job policy.
/// </summary>
public interface IMaintenanceWorker
{
    string Job { get; }

    Task<MaintenanceRunOutcome> RunAsync(
        MaintenanceCommand command,
        CancellationToken ct = default);
}
