namespace AethericForge.Runtime.Library.Abstractions.Custody;

public interface IKnowledgeMutator
{
    ValueTask UpdateAsync(
        KnowledgeReference reference,
        IKnowledgeObject replacement,
        CancellationToken cancellationToken = default);
}