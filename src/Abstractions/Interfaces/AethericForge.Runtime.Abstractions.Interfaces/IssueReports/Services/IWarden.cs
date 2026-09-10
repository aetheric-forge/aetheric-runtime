using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Services;

/// <summary>
/// Custody ledger for issue reports, scoped per <paramref name="domain"/> so unrelated apps never see
/// each other's reports.
/// </summary>
public interface IWarden : IAuthority<IIssueReportsClerk>
{
    Task<IssueReport> SubmitAsync(
        string domain,
        string title,
        string description,
        string reporterId,
        CancellationToken ct = default);

    Task<IReadOnlyList<IssueReport>> ListAsync(
        string domain,
        CancellationToken ct = default);

    Task<IssueReport?> GetAsync(
        string domain,
        Guid id,
        CancellationToken ct = default);

    Task<IssueReport?> UpdateStatusAsync(
        string domain,
        Guid id,
        IssueStatus status,
        string? resolutionNote = null,
        CancellationToken ct = default);
}
