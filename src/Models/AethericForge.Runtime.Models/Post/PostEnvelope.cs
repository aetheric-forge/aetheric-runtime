using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

namespace AethericForge.Runtime.Models.Post;

public sealed record PostEnvelope<TMessage> : IPostEnvelope<TMessage>
{
    public PostEnvelope(
        IPostReference reference,
        TMessage payload,
        IPostMetadata metadata)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        Payload = payload!;
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public IPostReference Reference { get; }
    public IPostMetadata Metadata { get; }
    public TMessage Payload { get; }

    object IPostEnvelope.Payload => Payload!;
}
