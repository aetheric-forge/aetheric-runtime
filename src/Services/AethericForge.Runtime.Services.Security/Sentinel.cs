using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;

namespace AethericForge.Runtime.Services.Security;

/// <summary>
/// Custody ledger for security records, backed directly by the Workbench institution's
/// WorkbenchService rather than a dedicated provider - same rationale as Maintenance's Caretaker and
/// Issue Reports' Warden. Unlike those two, every raise and status transition is also appended to a
/// separate, per-domain history catalog, so a record's full audit trail survives regardless of its
/// current status. Both catalogs are written under the same semaphore gate so they never diverge.
/// WorkbenchService exposes no distributed lock, same caveat as every other single-key-catalog
/// implementation in this codebase.
/// </summary>
public sealed class Sentinel(IWorkbenchService workbench, ITeam<ISecurityClerk> team) : ISentinel
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly IWorkbenchService _workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));

    public ITeam<ISecurityClerk> Team { get; } = team ?? throw new ArgumentNullException(nameof(team));

    public async Task<SecurityRecord> RaiseAsync(
        string domain,
        SecurityRecordKind kind,
        string title,
        string description,
        string reporterId,
        SecuritySeverity severity,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(reporterId);

        var now = DateTimeOffset.UtcNow;
        var record = new SecurityRecord(
            Guid.NewGuid(),
            domain,
            kind,
            title,
            description,
            reporterId,
            severity,
            SecurityStatus.Open,
            now);

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var catalog = await ReadRecordsAsync(domain, ct).ConfigureAwait(false);
            catalog.Add(record);
            await SaveRecordsAsync(domain, catalog, ct).ConfigureAwait(false);

            var history = await ReadHistoryAsync(domain, ct).ConfigureAwait(false);
            history.Add(new SecurityRecordEvent(
                Guid.NewGuid(),
                record.Id,
                PreviousStatus: null,
                NewStatus: SecurityStatus.Open,
                reporterId,
                Note: description,
                now));
            await SaveHistoryAsync(domain, history, ct).ConfigureAwait(false);

            return record;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<SecurityRecord>> ListAsync(
        string domain,
        CancellationToken ct = default)
    {
        return await ReadRecordsAsync(domain, ct).ConfigureAwait(false);
    }

    public async Task<SecurityRecord?> GetAsync(
        string domain,
        Guid id,
        CancellationToken ct = default)
    {
        var catalog = await ReadRecordsAsync(domain, ct).ConfigureAwait(false);
        return catalog.FirstOrDefault(record => record.Id == id);
    }

    public async Task<SecurityRecord?> UpdateStatusAsync(
        string domain,
        Guid id,
        SecurityStatus status,
        string actorId,
        string? note = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var catalog = await ReadRecordsAsync(domain, ct).ConfigureAwait(false);
            var index = catalog.FindIndex(record => record.Id == id);
            if (index < 0)
            {
                return null;
            }

            var previous = catalog[index];
            var now = DateTimeOffset.UtcNow;
            var isResolving = status is SecurityStatus.Resolved or SecurityStatus.Closed;
            var updated = previous with
            {
                Status = status,
                ResolutionNote = note,
                ResolvedAt = isResolving ? now : null
            };
            catalog[index] = updated;
            await SaveRecordsAsync(domain, catalog, ct).ConfigureAwait(false);

            var history = await ReadHistoryAsync(domain, ct).ConfigureAwait(false);
            history.Add(new SecurityRecordEvent(
                Guid.NewGuid(),
                id,
                previous.Status,
                status,
                actorId,
                note,
                now));
            await SaveHistoryAsync(domain, history, ct).ConfigureAwait(false);

            return updated;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<SecurityRecordEvent>> GetHistoryAsync(
        string domain,
        Guid id,
        CancellationToken ct = default)
    {
        var history = await ReadHistoryAsync(domain, ct).ConfigureAwait(false);
        return history.Where(evt => evt.RecordId == id).ToList();
    }

    private static string RecordsKey(string domain) => $"security-catalog/{RequireDomain(domain)}";

    private static string HistoryKey(string domain) => $"security-history/{RequireDomain(domain)}";

    private static string RequireDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("A domain is required.", nameof(domain));
        }

        return domain.Trim();
    }

    private async Task<List<SecurityRecord>> ReadRecordsAsync(string domain, CancellationToken ct)
    {
        var catalog = await _workbench.GetAsync<List<SecurityRecord>>(RecordsKey(domain), ct).ConfigureAwait(false);
        return catalog ?? [];
    }

    private Task SaveRecordsAsync(string domain, List<SecurityRecord> catalog, CancellationToken ct)
    {
        return _workbench.PutAsync(RecordsKey(domain), catalog, ct);
    }

    private async Task<List<SecurityRecordEvent>> ReadHistoryAsync(string domain, CancellationToken ct)
    {
        var history = await _workbench.GetAsync<List<SecurityRecordEvent>>(HistoryKey(domain), ct).ConfigureAwait(false);
        return history ?? [];
    }

    private Task SaveHistoryAsync(string domain, List<SecurityRecordEvent> history, CancellationToken ct)
    {
        return _workbench.PutAsync(HistoryKey(domain), history, ct);
    }
}
