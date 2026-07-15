using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;

using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;

public interface IPostProvider
{
    string Name { get; }

    Task PublishAsync(
        IPostEnvelope envelope,
        CancellationToken ct = default);

    Task SubscribeAsync(
        IPostReference reference,
        IMessageConsumer consumer,
        CancellationToken ct = default);
}
