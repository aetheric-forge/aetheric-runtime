using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;

public interface IIdentityLifecycle
{
    IIdentitySubject Subject { get; }
    IdentityState CurrentState { get; }
    IReadOnlyCollection<IIdentityTransition> Transitions { get; }
}