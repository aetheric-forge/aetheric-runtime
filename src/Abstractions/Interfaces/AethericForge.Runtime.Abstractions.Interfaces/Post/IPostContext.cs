namespace AethericForge.Runtime.Abstractions.Interfaces.Post;

public interface IPostContext
{
    IPostEnvelope Envelope { get; }
    IReadOnlyDictionary<string, string> Attributes { get; }

    Task PublishAsync<TMessage>(
        IPostReference reference,
        TMessage message,
        IPostMetadata? metadata = null,
        CancellationToken ct = default);
}
