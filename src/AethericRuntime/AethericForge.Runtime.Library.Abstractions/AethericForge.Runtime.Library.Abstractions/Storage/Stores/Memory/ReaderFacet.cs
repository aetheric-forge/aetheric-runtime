namespace AethericForge.Runtime.Library.Abstractions.Storage.Stores.Memory;

using AethericForge.Runtime.Library.Abstractions.Custody.Interfaces;

public class ReaderFacet
{
    private IKnowledgeStore _store;

    public ReaderFacet(IKnowledgeStore store)
    {
        this._store = store;
    }
}
