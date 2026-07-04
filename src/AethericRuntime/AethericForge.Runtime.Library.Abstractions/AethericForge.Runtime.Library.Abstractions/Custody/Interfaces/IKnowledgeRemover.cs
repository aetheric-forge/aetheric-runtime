namespace AethericForge.Runtime.Library.Abstractions.Custody;

public interface IKnowledgeRemover
{
    ValueTask RemoveAsync(
        KnowledgeReference reference,
        CancellationToken cancellationToken = default);
}