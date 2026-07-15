using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;

public interface IIdentityLifecyclePolicy
{
    string Name { get; }
    Task<bool> CanTransitionAsync(IIdentitySubject subject, IdentityState fromState, IdentityState toState, CancellationToken cancellationToken = default);
}