namespace AethericForge.Runtime.Library.Abstractions.Custody;

public interface IKnowledgeSearcher
{
    IAsyncEnumerable<KnowledgeReference> SearchAsync(
        KnowledgeQuery query,
        CancellationToken cancellationToken = default);
}