using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Core;
using AethericForge.Runtime.Abstractions.Interfaces.Storage;

namespace AethericForge.Runtime.Institutions.Library;

public sealed class Shelf
{
    public Shelf(string name)
        : this(
            name,
            new MemoryStore<string, IKnowledgeObject>(),
            new MemoryStore<string, IKnowledgeReference>(),
            new MemoryStore<string, IStorageReference>())
    {
    }

    public Shelf(
        string name,
        IStore<string, IKnowledgeObject> store,
        IStore<string, IKnowledgeReference> knowledgeReferences,
        IStore<string, IStorageReference> storageReferences)
    {
        Name = NormalizeName(name);
        Store = store ?? throw new ArgumentNullException(nameof(store));
        KnowledgeReferences = knowledgeReferences ?? throw new ArgumentNullException(nameof(knowledgeReferences));
        StorageReferences = storageReferences ?? throw new ArgumentNullException(nameof(storageReferences));
    }

    public string Name { get; }
    public IStore<string, IKnowledgeObject> Store { get; }
    public IStore<string, IKnowledgeReference> KnowledgeReferences { get; }
    public IStore<string, IStorageReference> StorageReferences { get; }

    public async Task<ShelfLocation> PlaceAsync(
        IKnowledgeObject knowledgeObject,
        IStorageReference storageReference,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(knowledgeObject);
        ArgumentNullException.ThrowIfNull(storageReference);

        var key = CreateKey(knowledgeObject.Reference);

        await Store.SetAsync(key, knowledgeObject, ct);
        await KnowledgeReferences.SetAsync(key, knowledgeObject.Reference, ct);
        await StorageReferences.SetAsync(key, storageReference, ct);

        return new ShelfLocation(Name, knowledgeObject.Reference, storageReference);
    }

    public async Task<IKnowledgeObject?> GetAsync(IKnowledgeReference knowledgeReference, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(knowledgeReference);
        return await Store.GetAsync(CreateKey(knowledgeReference), ct);
    }

    public async Task<bool> ExistsAsync(IKnowledgeReference knowledgeReference, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(knowledgeReference);
        return await KnowledgeReferences.ExistsAsync(CreateKey(knowledgeReference), ct);
    }

    public async Task<ShelfLocation?> LocateAsync(IKnowledgeReference knowledgeReference, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(knowledgeReference);

        var key = CreateKey(knowledgeReference);
        var storedReference = await KnowledgeReferences.GetAsync(key, ct);
        var storageReference = await StorageReferences.GetAsync(key, ct);

        return storedReference is null || storageReference is null
            ? null
            : new ShelfLocation(Name, storedReference, storageReference);
    }

    public async Task<bool> RemoveAsync(IKnowledgeReference knowledgeReference, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(knowledgeReference);

        var key = CreateKey(knowledgeReference);
        var removedObject = await Store.RemoveAsync(key, ct);
        var removedKnowledgeReference = await KnowledgeReferences.RemoveAsync(key, ct);
        var removedStorageReference = await StorageReferences.RemoveAsync(key, ct);

        return removedObject || removedKnowledgeReference || removedStorageReference;
    }

    private static string CreateKey(IKnowledgeReference reference)
    {
        return string.Join(
            ':',
            reference.Set,
            reference.Kind,
            reference.Name,
            reference.Version,
            reference.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            reference.ContentHash ?? string.Empty);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Shelf name is required.", nameof(name));
        }

        return name.Trim();
    }
}
