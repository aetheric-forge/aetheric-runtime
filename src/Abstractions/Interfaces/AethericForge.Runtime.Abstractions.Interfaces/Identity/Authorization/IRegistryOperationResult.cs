namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;

public interface IRegistryOperationResult
{
    RegistryOperationStatus Status { get; }
    string? FailureReason { get; }
}

public interface IRegistryOperationResult<out TValue> : IRegistryOperationResult
{
    TValue? Value { get; }
}
