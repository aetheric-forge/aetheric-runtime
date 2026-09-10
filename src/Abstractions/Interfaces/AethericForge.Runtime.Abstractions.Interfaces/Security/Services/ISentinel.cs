using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Security.Services;

/// <summary>
/// Custody ledger for security records (incidents and audit findings, unified), scoped per
/// <paramref name="domain"/> so unrelated apps never see each other's records. Every raise and status
/// transition is preserved in an append-only history - see <see cref="GetHistoryAsync"/> - so a
/// record's full traceability survives regardless of its current status.
/// </summary>
public interface ISentinel : IAuthority<ISecurityClerk>
{
    Task<SecurityRecord> RaiseAsync(
        string domain,
        SecurityRecordKind kind,
        string title,
        string description,
        string reporterId,
        SecuritySeverity severity,
        CancellationToken ct = default);

    Task<IReadOnlyList<SecurityRecord>> ListAsync(
        string domain,
        CancellationToken ct = default);

    Task<SecurityRecord?> GetAsync(
        string domain,
        Guid id,
        CancellationToken ct = default);

    Task<SecurityRecord?> UpdateStatusAsync(
        string domain,
        Guid id,
        SecurityStatus status,
        string actorId,
        string? note = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<SecurityRecordEvent>> GetHistoryAsync(
        string domain,
        Guid id,
        CancellationToken ct = default);
}
