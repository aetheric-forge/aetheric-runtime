using AethericForge.Runtime.Abstractions.Interfaces.TableTop.Primitives;
namespace AethericForge.Runtime.Abstractions.Interfaces.TableTop.Consumers;

public interface ITableTopEventConsumer
{
    Task ConsumeAsync(ITableTopEvent tableTopEvent, CancellationToken ct = default);
}
