namespace AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Primitives;

public sealed record IssueReport(
    Guid Id,
    string Domain,
    string Title,
    string Description,
    string ReporterId,
    IssueStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ResolvedAt = null,
    string? ResolutionNote = null);

public enum IssueStatus
{
    Open,
    InProgress,
    Resolved,
    WontFix
}
