namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

public interface IKnowledgeObject
{
    IKnowledgeReference Reference { get; }
    IKnowledgeDescriptor Descriptor { get; }
    KnowledgeLifecycle Lifecycle { get; }
    KnowledgeState State { get; }
    DateTimeOffset CreatedAtUtc { get; }
    DateTimeOffset UpdatedAtUtc { get; }
}
