namespace AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Primitives;

public sealed record MaintenanceCommand(
    Guid Id,
    string Domain,
    string Job,
    DateTimeOffset RequestedAtUtc,
    string Source);

public enum MaintenanceRunStatus
{
    Completed,
    Partial,
    Failed
}

public sealed record MaintenanceRunOutcome(
    Guid CommandId,
    MaintenanceRunStatus Status,
    int ProcessedCount,
    int RemainingCount,
    DateTimeOffset OccurredAtUtc,
    string? FailureReason = null);

public sealed record MaintenanceRunRecord(
    MaintenanceCommand Command,
    MaintenanceRunOutcome? Outcome,
    bool IsCollected);

public enum MaintenancePostStatus
{
    Accepted,
    AlreadyAccepted
}

public sealed record MaintenancePostResult(
    MaintenancePostStatus Status,
    MaintenanceCommand Command);
