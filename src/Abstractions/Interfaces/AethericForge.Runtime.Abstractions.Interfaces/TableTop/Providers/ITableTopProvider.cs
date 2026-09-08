using AethericForge.Runtime.Abstractions.Interfaces.TableTop.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.TableTop.Consumers;
namespace AethericForge.Runtime.Abstractions.Interfaces.TableTop.Providers;

public interface ITableTopProvider
{
    string Name { get; }
    Task<ITableTopState?> GetStateAsync(ITableTopReference tableTop, CancellationToken ct = default);
    Task SetActiveSceneAsync(ITableTopReference tableTop, string sceneId, string operationId, CancellationToken ct = default);
    Task<IAsyncDisposable> SubscribeAsync(ITableTopReference tableTop, ITableTopEventConsumer consumer, CancellationToken ct = default);
}
