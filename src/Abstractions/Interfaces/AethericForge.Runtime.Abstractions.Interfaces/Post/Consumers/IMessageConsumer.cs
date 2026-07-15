using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;

using AethericForge.Runtime.Abstractions.Interfaces.Post;

public interface IMessageConsumer
{
    IPostContract Contract { get; }

    Task ConsumeAsync(
        IPostEnvelope envelope,
        IPostContext context,
        CancellationToken ct = default);
}

public interface IMessageConsumer<in TMessage> : IMessageConsumer
{
    Task ConsumeAsync(
        TMessage message,
        IPostContext context,
        CancellationToken ct = default);
}
