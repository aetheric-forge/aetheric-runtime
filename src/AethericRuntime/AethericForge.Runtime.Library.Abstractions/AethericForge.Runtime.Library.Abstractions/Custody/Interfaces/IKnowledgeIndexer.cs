namespace AethericForge.Runtime.Library.Abstractions.Custody.Interfaces;

public interface IKnowledgeIndexer
{
    ValueTask IndexAsync(
        IKnowledgeObject knowledge,
        CancellationToken cancellationToken = default);

    ValueTask DeindexAsync(
        KnowledgeReference reference,
        CancellationToken cancellationToken = default);

    ValueTask ReindexAsync(
        IKnowledgeObject knowledge,
        CancellationToken cancellationToken = default);
}