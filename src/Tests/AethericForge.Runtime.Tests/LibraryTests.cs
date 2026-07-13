using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Institutions.Library;
using AethericForge.Runtime.Models.Archive;
using AethericForge.Runtime.Models.Knowledge.Primitives;

namespace AethericForge.Runtime.Tests;

public class LibraryTests
{
    [Fact]
    public void Library_Can_Create_And_Find_Shelf()
    {
        var library = new Library();

        var shelf = library.CreateShelf("general");

        Assert.True(library.ContainsShelf("general"));
        Assert.Same(shelf, library.GetShelf("general"));
        Assert.Single(library.Shelves);
    }

    [Fact]
    public async Task Shelf_Place_Locate_Existence_And_Removal_Work_For_KnowledgeObject()
    {
        var shelf = new Shelf("reference");
        var knowledgeObject = CreateKnowledgeObject();
        var archiveReference = new ArchiveReference("memory", "reference/article-one");

        var placement = await shelf.PlaceAsync(knowledgeObject, archiveReference);

        Assert.Equal("reference", placement.ShelfName);
        Assert.Same(knowledgeObject.Reference, placement.KnowledgeReference);
        Assert.Same(archiveReference, placement.ArchiveReference);
        Assert.True(await shelf.ExistsAsync(knowledgeObject.Reference));

        var located = await shelf.LocateAsync(knowledgeObject.Reference);
        Assert.NotNull(located);
        Assert.Equal("reference", located.ShelfName);
        Assert.Same(knowledgeObject.Reference, located.KnowledgeReference);
        Assert.Same(archiveReference, located.ArchiveReference);

        var retrieved = await shelf.GetAsync(knowledgeObject.Reference);
        Assert.Same(knowledgeObject, retrieved);

        Assert.True(await shelf.RemoveAsync(knowledgeObject.Reference));
        Assert.False(await shelf.ExistsAsync(knowledgeObject.Reference));
        Assert.Null(await shelf.LocateAsync(knowledgeObject.Reference));
        Assert.Null(await shelf.GetAsync(knowledgeObject.Reference));
        Assert.False(await shelf.RemoveAsync(knowledgeObject.Reference));
    }

    [Fact]
    public async Task MemoryStore_Supports_Set_Get_Exists_Remove_And_Clear()
    {
        var store = new MemoryStore<string, int>();

        Assert.False(await store.ExistsAsync("answer"));

        await store.SetAsync("answer", 42);

        Assert.True(await store.ExistsAsync("answer"));
        Assert.Equal(42, await store.GetAsync("answer"));
        Assert.True(await store.RemoveAsync("answer"));
        Assert.False(await store.ExistsAsync("answer"));

        await store.SetAsync("answer", 42);
        await store.ClearAsync();

        Assert.False(await store.ExistsAsync("answer"));
    }

    private static TestKnowledgeObject CreateKnowledgeObject()
    {
        return new TestKnowledgeObject(
            new KnowledgeReference("runtime", "article", "knowledge", "1.0.0", 1),
            new KnowledgeDescriptor("Knowledge"));
    }

    private sealed class TestKnowledgeObject : KnowledgeObjectBase
    {
        public TestKnowledgeObject(IKnowledgeReference reference, IKnowledgeDescriptor descriptor)
            : base(reference, descriptor)
        {
        }
    }
}
