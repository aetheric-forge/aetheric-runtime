using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;

namespace AethericForge.Runtime.Models.Identity.Directory;

public sealed record ExternalDirectoryResult<TValue> : IExternalDirectoryResult<TValue>
{
    private ExternalDirectoryResult(
        ExternalDirectoryStatus status,
        TValue? value,
        DateTimeOffset observedAtUtc,
        DateTimeOffset? freshUntilUtc,
        string? failureReason)
    {
        if (freshUntilUtc < observedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(freshUntilUtc),
                freshUntilUtc,
                "Freshness cannot end before the observation time.");
        }

        if (status == ExternalDirectoryStatus.Success && value is null)
        {
            throw new ArgumentException("A successful directory result requires a value.", nameof(value));
        }

        if (status != ExternalDirectoryStatus.Success && value is not null)
        {
            throw new ArgumentException("A failed directory result cannot contain a value.", nameof(value));
        }

        Status = status;
        Value = value;
        ObservedAtUtc = observedAtUtc;
        FreshUntilUtc = freshUntilUtc;
        FailureReason = DirectoryValue.NormalizeOptional(failureReason);
    }

    public ExternalDirectoryStatus Status { get; }
    public TValue? Value { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public DateTimeOffset? FreshUntilUtc { get; }
    public string? FailureReason { get; }

    public static ExternalDirectoryResult<TValue> Success(
        TValue value,
        DateTimeOffset observedAtUtc,
        DateTimeOffset? freshUntilUtc = null) =>
        new(ExternalDirectoryStatus.Success, value, observedAtUtc, freshUntilUtc, null);

    public static ExternalDirectoryResult<TValue> Failure(
        ExternalDirectoryStatus status,
        DateTimeOffset observedAtUtc,
        string? failureReason = null)
    {
        if (status == ExternalDirectoryStatus.Success)
        {
            throw new ArgumentException("Use Success to create a successful result.", nameof(status));
        }

        return new(status, default, observedAtUtc, null, failureReason);
    }
}
