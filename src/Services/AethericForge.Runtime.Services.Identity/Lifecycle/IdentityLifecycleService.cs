using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Identity.Lifecycle;
using System.Collections.Concurrent;

namespace AethericForge.Runtime.Services.Identity.Lifecycle;

public sealed class IdentityLifecycleService : IIdentityLifecycleService
{
    private sealed class LifecycleEntry
    {
        public IdentityState State { get; set; }
        public List<IIdentityTransition> Transitions { get; } = new();
    }

    private readonly ConcurrentDictionary<string, LifecycleEntry> _lifecycles = new();
    private readonly IEnumerable<IIdentityLifecyclePolicy> _policies;

    public IdentityLifecycleService(IEnumerable<IIdentityLifecyclePolicy> policies)
    {
        _policies = policies ?? [];
    }

    public Task<IIdentityLifecycle> GetLifecycleAsync(IIdentitySubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var key = GetKey(subject);
        var entry = _lifecycles.GetOrAdd(key, _ => new LifecycleEntry { State = subject.State });

        lock (entry)
        {
            return Task.FromResult<IIdentityLifecycle>(new IdentityLifecycle(subject, entry.State, entry.Transitions.ToArray()));
        }
    }

    public async Task TransitionAsync(IIdentitySubject subject, IdentityState newState, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var key = GetKey(subject);
        var entry = _lifecycles.GetOrAdd(key, _ => new LifecycleEntry { State = subject.State });

        IdentityState currentState;
        lock (entry)
        {
            currentState = entry.State;
        }

        if (currentState == newState)
        {
            return;
        }

        foreach (var policy in _policies)
        {
            if (!await policy.CanTransitionAsync(subject, currentState, newState, cancellationToken))
            {
                throw new InvalidOperationException($"Transition from {currentState} to {newState} is denied by policy '{policy.Name}'.");
            }
        }

        lock (entry)
        {
            var transition = new IdentityTransition(entry.State, newState, DateTimeOffset.UtcNow, reason);
            entry.Transitions.Add(transition);
            entry.State = newState;
        }
    }

    private static string GetKey(IIdentitySubject subject) => $"{subject.Scheme}:{subject.SubjectId}";
}
