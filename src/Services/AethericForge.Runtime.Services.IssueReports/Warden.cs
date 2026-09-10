using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;

namespace AethericForge.Runtime.Services.IssueReports;

/// <summary>
/// Custody ledger for issue reports, backed directly by the Workbench institution's WorkbenchService
/// rather than a dedicated provider - an issue report catalog is exactly the "private, provisional
/// work" Workbench already exists for. The whole per-domain catalog is stored as a single value under
/// one key (WorkbenchService is a plain key/value store, not a queryable one), so writes are
/// serialized with an in-process semaphore around each read-modify-write cycle - WorkbenchService
/// exposes no distributed lock, same caveat as every other single-key-catalog implementation in this
/// codebase.
/// </summary>
public sealed class Warden(IWorkbenchService workbench, ITeam<IIssueReportsClerk> team) : IWarden
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly IWorkbenchService _workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));

    public ITeam<IIssueReportsClerk> Team { get; } = team ?? throw new ArgumentNullException(nameof(team));

    public async Task<IssueReport> SubmitAsync(
        string domain,
        string title,
        string description,
        string reporterId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(reporterId);

        var report = new IssueReport(
            Guid.NewGuid(),
            domain,
            title,
            description,
            reporterId,
            IssueStatus.Open,
            DateTimeOffset.UtcNow);

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var catalog = await ReadAsync(domain, ct).ConfigureAwait(false);
            catalog.Add(report);
            await SaveAsync(domain, catalog, ct).ConfigureAwait(false);
            return report;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<IssueReport>> ListAsync(
        string domain,
        CancellationToken ct = default)
    {
        return await ReadAsync(domain, ct).ConfigureAwait(false);
    }

    public async Task<IssueReport?> GetAsync(
        string domain,
        Guid id,
        CancellationToken ct = default)
    {
        var catalog = await ReadAsync(domain, ct).ConfigureAwait(false);
        return catalog.FirstOrDefault(report => report.Id == id);
    }

    public async Task<IssueReport?> UpdateStatusAsync(
        string domain,
        Guid id,
        IssueStatus status,
        string? resolutionNote = null,
        CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var catalog = await ReadAsync(domain, ct).ConfigureAwait(false);
            var index = catalog.FindIndex(report => report.Id == id);
            if (index < 0)
            {
                return null;
            }

            var isResolving = status is IssueStatus.Resolved or IssueStatus.WontFix;
            var updated = catalog[index] with
            {
                Status = status,
                ResolutionNote = resolutionNote,
                ResolvedAt = isResolving ? DateTimeOffset.UtcNow : null
            };
            catalog[index] = updated;
            await SaveAsync(domain, catalog, ct).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string Key(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("A domain is required.", nameof(domain));
        }

        return $"issue-reports-catalog/{domain.Trim()}";
    }

    private async Task<List<IssueReport>> ReadAsync(string domain, CancellationToken ct)
    {
        var catalog = await _workbench.GetAsync<List<IssueReport>>(Key(domain), ct).ConfigureAwait(false);
        return catalog ?? [];
    }

    private Task SaveAsync(string domain, List<IssueReport> catalog, CancellationToken ct)
    {
        return _workbench.PutAsync(Key(domain), catalog, ct);
    }
}
