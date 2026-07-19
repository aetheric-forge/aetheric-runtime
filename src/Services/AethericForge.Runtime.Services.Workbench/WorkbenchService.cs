using System.Collections.Concurrent;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;

namespace AethericForge.Runtime.Services.Workbench;

public sealed class WorkbenchService : IWorkbenchService
{
    private readonly ConcurrentDictionary<WorkKey, object?> _work = new();
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Guid, Func<object?, CancellationToken, Task>>> _receivers = new();

    public async Task PutAsync<TWork>(
        object key,
        TWork work,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(work);
        ct.ThrowIfCancellationRequested();

        _work[new WorkKey(typeof(TWork), key)] = work;

        if (!_receivers.TryGetValue(typeof(TWork), out var receivers))
        {
            return;
        }

        foreach (var receiver in receivers.Values)
        {
            await receiver(work, ct).ConfigureAwait(false);
        }
    }

    public Task<TWork?> GetAsync<TWork>(
        object key,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(
            _work.TryGetValue(new WorkKey(typeof(TWork), key), out var work)
                ? (TWork?)work
                : default);
    }

    public IDisposable Subscribe<TWork>(
        Func<TWork, CancellationToken, Task> receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        var id = Guid.NewGuid();
        var receivers = _receivers.GetOrAdd(
            typeof(TWork),
            static _ => new ConcurrentDictionary<Guid, Func<object?, CancellationToken, Task>>());

        receivers[id] = (work, ct) => receiver((TWork)work!, ct);
        return new Subscription(() => receivers.TryRemove(id, out _));
    }

    private readonly record struct WorkKey(Type WorkType, object Key);

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose()
        {
            Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}
