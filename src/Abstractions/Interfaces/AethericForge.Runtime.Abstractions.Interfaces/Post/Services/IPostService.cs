using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Post.Services;

using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;

public interface IPostService
{
    Task PublishAsync<TMessage>(
        IPostReference reference,
        TMessage message,
        IPostMetadata? metadata = null,
        CancellationToken ct = default);

    Task SubscribeAsync<TMessage>(
        IPostReference reference,
        IMessageConsumer<TMessage> consumer,
        CancellationToken ct = default);
}
