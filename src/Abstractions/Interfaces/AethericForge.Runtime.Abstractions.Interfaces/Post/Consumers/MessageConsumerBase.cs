using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;

using AethericForge.Runtime.Abstractions.Interfaces.Post;

public abstract class MessageConsumerBase<TMessage> : IMessageConsumer<TMessage>
{
    public abstract IPostContract Contract { get; }

    public Task ConsumeAsync(
        IPostEnvelope envelope,
        IPostContext context,
        CancellationToken ct = default)
    {
        if (envelope is IPostEnvelope<TMessage> typedEnvelope)
        {
            return ConsumeAsync(typedEnvelope.Payload, context, ct);
        }

        if (envelope.Payload is TMessage message)
        {
            return ConsumeAsync(message, context, ct);
        }

        throw new InvalidOperationException(
            $"Envelope message must be assignable to {typeof(TMessage).FullName}.");
    }

    public abstract Task ConsumeAsync(
        TMessage message,
        IPostContext context,
        CancellationToken ct = default);
}
