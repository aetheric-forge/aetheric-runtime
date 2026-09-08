using AethericForge.Runtime.Abstractions.Interfaces.TableTop.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.TableTop.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.TableTop.Consumers;

namespace AethericForge.Runtime.Models.TableTop;

public abstract class TableTopProvider : ITableTopProvider
{
    protected TableTopProvider(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
    public string Name { get; }
    public Task<ITableTopState?> GetStateAsync(ITableTopReference tableTop, CancellationToken ct = default)
    {
        Validate(tableTop, ct);
        return GetStateCoreAsync(tableTop, ct);
    }
    public Task SetActiveSceneAsync(ITableTopReference tableTop, string sceneId, string operationId, CancellationToken ct = default)
    {
        Validate(tableTop, ct);
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        return SetActiveSceneCoreAsync(tableTop, sceneId, operationId, ct);
    }
    protected abstract Task<ITableTopState?> GetStateCoreAsync(ITableTopReference tableTop, CancellationToken ct);
    protected abstract Task SetActiveSceneCoreAsync(ITableTopReference tableTop, string sceneId, string operationId, CancellationToken ct);

    public Task<IAsyncDisposable> SubscribeAsync(ITableTopReference tableTop, ITableTopEventConsumer consumer, CancellationToken ct = default)
    {
        Validate(tableTop, ct);
        ArgumentNullException.ThrowIfNull(consumer);
        return SubscribeCoreAsync(tableTop, consumer, ct);
    }
    protected abstract Task<IAsyncDisposable> SubscribeCoreAsync(ITableTopReference tableTop, ITableTopEventConsumer consumer, CancellationToken ct);
    private void Validate(ITableTopReference reference, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.Scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.Id);
        if (!string.Equals(Name, reference.Provider, StringComparison.Ordinal))
            throw new ArgumentException("Reference belongs to a different provider.", nameof(reference));
    }
}
