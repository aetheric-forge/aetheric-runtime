using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Services;

/// <summary>
/// Custody ledger for maintenance commands, scoped per <paramref name="domain"/> so unrelated apps or
/// job families never see each other's commands. Accepting a command records custody of the request,
/// not successful execution; <see cref="RecordOutcomeAsync"/> is what records processing outcomes.
/// </summary>
public interface ICaretaker : IAuthority<IMaintenanceClerk>
{
    Task<MaintenancePostResult> PostAsync(
        string domain,
        MaintenanceCommand command,
        CancellationToken ct = default);

    Task<MaintenanceCommand?> CollectNextAsync(
        string domain,
        string job,
        CancellationToken ct = default);

    Task RecordOutcomeAsync(
        string domain,
        MaintenanceRunOutcome outcome,
        CancellationToken ct = default);

    Task<IReadOnlyList<MaintenanceRunRecord>> ListRunsAsync(
        string domain,
        CancellationToken ct = default);
}
