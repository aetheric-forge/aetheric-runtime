using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Models.Identity.Lifecycle;

public sealed record IdentityLifecycle : IIdentityLifecycle
{
    public IdentityLifecycle(
        IIdentitySubject subject,
        IdentityState currentState,
        IEnumerable<IIdentityTransition>? transitions = null)
    {
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        CurrentState = currentState;
        Transitions = transitions?.ToArray() ?? [];
    }

    public IIdentitySubject Subject { get; }
    public IdentityState CurrentState { get; }
    public IReadOnlyCollection<IIdentityTransition> Transitions { get; }
}
