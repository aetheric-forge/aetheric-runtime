using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;

public interface IIdentityLifecycleService
{
    Task<IIdentityLifecycle> GetLifecycleAsync(IIdentitySubject subject, CancellationToken cancellationToken = default);
    Task TransitionAsync(IIdentitySubject subject, IdentityState newState, string? reason = null, CancellationToken cancellationToken = default);
}