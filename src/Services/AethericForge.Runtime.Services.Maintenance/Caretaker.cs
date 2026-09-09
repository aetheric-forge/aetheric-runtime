using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;

namespace AethericForge.Runtime.Services.Maintenance;

/// <summary>
/// Custody ledger for maintenance commands, backed directly by the Workbench institution's
/// WorkbenchService rather than a dedicated provider - a maintenance ledger is exactly the "private,
/// provisional work" Workbench already exists for. The whole per-domain ledger is stored as a single
/// value under one key (WorkbenchService is a plain key/value store, not a queryable one), so writes
/// are serialized with an in-process semaphore around each read-modify-write cycle - WorkbenchService
/// exposes no distributed lock, same caveat as every other single-key-catalog implementation in this
/// codebase.
/// </summary>
public sealed class Caretaker(IWorkbenchService workbench, ITeam<IMaintenanceClerk> team) : ICaretaker
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly IWorkbenchService _workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));

    public ITeam<IMaintenanceClerk> Team { get; } = team ?? throw new ArgumentNullException(nameof(team));

    public async Task<MaintenancePostResult> PostAsync(
        string domain,
        MaintenanceCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var catalog = await ReadAsync(domain, ct).ConfigureAwait(false);
            var existing = catalog.FirstOrDefault(record => record.Command.Id == command.Id);
            if (existing is not null)
            {
                return new MaintenancePostResult(MaintenancePostStatus.AlreadyAccepted, existing.Command);
            }

            catalog.Add(new MaintenanceRunRecord(command, null, false));
            await SaveAsync(domain, catalog, ct).ConfigureAwait(false);
            return new MaintenancePostResult(MaintenancePostStatus.Accepted, command);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<MaintenanceCommand?> CollectNextAsync(
        string domain,
        string job,
        CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var catalog = await ReadAsync(domain, ct).ConfigureAwait(false);
            var index = catalog.FindIndex(record => record.Command.Job == job && !record.IsCollected);
            if (index < 0)
            {
                return null;
            }

            var record = catalog[index];
            catalog[index] = record with { IsCollected = true };
            await SaveAsync(domain, catalog, ct).ConfigureAwait(false);
            return record.Command;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task RecordOutcomeAsync(
        string domain,
        MaintenanceRunOutcome outcome,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var catalog = await ReadAsync(domain, ct).ConfigureAwait(false);
            var index = catalog.FindIndex(record => record.Command.Id == outcome.CommandId);
            if (index < 0)
            {
                return;
            }

            catalog[index] = catalog[index] with { Outcome = outcome };
            await SaveAsync(domain, catalog, ct).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<MaintenanceRunRecord>> ListRunsAsync(
        string domain,
        CancellationToken ct = default)
    {
        return await ReadAsync(domain, ct).ConfigureAwait(false);
    }

    private static string Key(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("A domain is required.", nameof(domain));
        }

        return $"maintenance-catalog/{domain.Trim()}";
    }

    private async Task<List<MaintenanceRunRecord>> ReadAsync(string domain, CancellationToken ct)
    {
        var catalog = await _workbench.GetAsync<List<MaintenanceRunRecord>>(Key(domain), ct).ConfigureAwait(false);
        return catalog ?? [];
    }

    private Task SaveAsync(string domain, List<MaintenanceRunRecord> catalog, CancellationToken ct)
    {
        return _workbench.PutAsync(Key(domain), catalog, ct);
    }
}
