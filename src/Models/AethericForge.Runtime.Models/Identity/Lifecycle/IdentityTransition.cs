using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;

namespace AethericForge.Runtime.Models.Identity.Lifecycle;

public sealed record IdentityTransition : IIdentityTransition
{
    public IdentityTransition(
        IdentityState fromState,
        IdentityState toState,
        DateTimeOffset timestamp,
        string? reason = null)
    {
        FromState = fromState;
        ToState = toState;
        Timestamp = timestamp;
        Reason = reason;
    }

    public IdentityState FromState { get; }
    public IdentityState ToState { get; }
    public DateTimeOffset Timestamp { get; }
    public string? Reason { get; }
}
