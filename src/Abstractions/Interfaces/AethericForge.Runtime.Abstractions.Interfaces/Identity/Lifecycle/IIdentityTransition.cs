namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;

public interface IIdentityTransition
{
    IdentityState FromState { get; }
    IdentityState ToState { get; }
    DateTimeOffset Timestamp { get; }
    string? Reason { get; }
}