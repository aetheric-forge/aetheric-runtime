namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;

public interface IExternalDirectoryResult<out TValue>
{
    ExternalDirectoryStatus Status { get; }
    TValue? Value { get; }
    DateTimeOffset ObservedAtUtc { get; }
    DateTimeOffset? FreshUntilUtc { get; }
    string? FailureReason { get; }
}
