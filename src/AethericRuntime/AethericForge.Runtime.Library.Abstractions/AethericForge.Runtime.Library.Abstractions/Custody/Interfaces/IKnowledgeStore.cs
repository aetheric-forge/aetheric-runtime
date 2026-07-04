namespace AethericForge.Runtime.Library.Abstractions.Custody;

public interface IKnowledgeStore
{
    ValueTask<bool> ContainsAsync(
        KnowledgeIdentifier identifier,
        CancellationToken cancellationToken = default);

    ValueTask<IKnowledgeObject?> ResolveAsync(
        KnowledgeReference reference,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IKnowledgeObject> EnumerateAsync(
        CancellationToken cancellationToken = default);

    public IKnowledgeReader Reader { get; }

    public IKnowledgeMutator Mutator { get; }

    public IKnowledgeRemover Remover { get; }

    public IKnowledgeSearcher Searcher { get; }

    public IKnowledgeIndexer Indexer { get; }

    public IKnowledgeAppender Appender { get; }
}