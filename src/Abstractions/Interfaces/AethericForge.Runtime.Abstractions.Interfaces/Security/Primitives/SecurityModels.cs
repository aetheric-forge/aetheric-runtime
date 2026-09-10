namespace AethericForge.Runtime.Abstractions.Interfaces.Security.Primitives;

public sealed record SecurityRecord(
    Guid Id,
    string Domain,
    SecurityRecordKind Kind,
    string Title,
    string Description,
    string ReporterId,
    SecuritySeverity Severity,
    SecurityStatus Status,
    DateTimeOffset RaisedAt,
    DateTimeOffset? ResolvedAt = null,
    string? ResolutionNote = null);

/// <summary>
/// One row per state transition, including the record's creation (<see cref="PreviousStatus"/> is
/// null there) - append-only, never mutated or removed, so the full history survives regardless of
/// the record's current status. This is the audit trail: every raise or status update appends one.
/// </summary>
public sealed record SecurityRecordEvent(
    Guid Id,
    Guid RecordId,
    SecurityStatus? PreviousStatus,
    SecurityStatus NewStatus,
    string ActorId,
    string? Note,
    DateTimeOffset OccurredAt);

public enum SecurityRecordKind
{
    Incident,
    AuditFinding
}

public enum SecuritySeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum SecurityStatus
{
    Open,
    Investigating,
    Resolved,
    Closed
}
