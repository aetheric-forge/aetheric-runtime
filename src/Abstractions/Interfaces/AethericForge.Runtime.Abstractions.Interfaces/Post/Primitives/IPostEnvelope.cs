namespace AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;

public interface IPostEnvelope
{
    IPostReference Reference { get; }
    IPostMetadata Metadata { get; }
    object Payload { get; }
}

public interface IPostEnvelope<out TMessage> : IPostEnvelope
{
    new TMessage Payload { get; }
}
