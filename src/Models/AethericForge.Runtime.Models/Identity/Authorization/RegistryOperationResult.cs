using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;
using AethericForge.Runtime.Models.Identity.Directory;

namespace AethericForge.Runtime.Models.Identity.Authorization;

public sealed record RegistryOperationResult : IRegistryOperationResult
{
    private RegistryOperationResult(RegistryOperationStatus status, string? failureReason)
    {
        if (status != RegistryOperationStatus.Succeeded && failureReason is null)
        {
            throw new ArgumentException("A failed operation result requires a failure reason.", nameof(failureReason));
        }

        Status = status;
        FailureReason = DirectoryValue.NormalizeOptional(failureReason);
    }

    public RegistryOperationStatus Status { get; }
    public string? FailureReason { get; }

    public static RegistryOperationResult Succeeded() => new(RegistryOperationStatus.Succeeded, null);

    public static RegistryOperationResult Failure(RegistryOperationStatus status, string failureReason)
    {
        if (status == RegistryOperationStatus.Succeeded)
        {
            throw new ArgumentException("Use Succeeded to create a successful result.", nameof(status));
        }

        return new(status, failureReason);
    }
}

public sealed record RegistryOperationResult<TValue> : IRegistryOperationResult<TValue>
{
    private RegistryOperationResult(RegistryOperationStatus status, TValue? value, string? failureReason)
    {
        if (status == RegistryOperationStatus.Succeeded && value is null)
        {
            throw new ArgumentException("A successful operation result requires a value.", nameof(value));
        }

        if (status != RegistryOperationStatus.Succeeded)
        {
            if (value is not null)
            {
                throw new ArgumentException("A failed operation result cannot contain a value.", nameof(value));
            }

            if (failureReason is null)
            {
                throw new ArgumentException("A failed operation result requires a failure reason.", nameof(failureReason));
            }
        }

        Status = status;
        Value = value;
        FailureReason = DirectoryValue.NormalizeOptional(failureReason);
    }

    public RegistryOperationStatus Status { get; }
    public TValue? Value { get; }
    public string? FailureReason { get; }

    public static RegistryOperationResult<TValue> Succeeded(TValue value) =>
        new(RegistryOperationStatus.Succeeded, value, null);

    public static RegistryOperationResult<TValue> Failure(RegistryOperationStatus status, string failureReason)
    {
        if (status == RegistryOperationStatus.Succeeded)
        {
            throw new ArgumentException("Use Succeeded to create a successful result.", nameof(status));
        }

        return new(status, default, failureReason);
    }
}
