using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Institutions.Library;
using AethericForge.Runtime.Models.Archive.Primitives;
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
    public void Library_CreateShelf_Throws_When_Already_Exists()
    {
        var library = new Library();
        library.CreateShelf("general");

        Assert.Throws<InvalidOperationException>(() => library.CreateShelf("general"));
    }

    [Fact]
    public void Library_GetOrCreateShelf_Returns_Existing_If_Found()
    {
        var library = new Library();
        var original = library.CreateShelf("general");
        var retrieved = library.GetOrCreateShelf("general");

        Assert.Same(original, retrieved);
    }

    [Fact]
    public void Library_GetOrCreateShelf_Creates_If_Missing()
    {
        var library = new Library();
        var created = library.GetOrCreateShelf("general");

        Assert.NotNull(created);
        Assert.True(library.ContainsShelf("general"));
    }

    [Fact]
    public void Library_RemoveShelf_Works()
    {
        var library = new Library();
        library.CreateShelf("general");

        Assert.True(library.RemoveShelf("general"));
        Assert.False(library.ContainsShelf("general"));
        Assert.False(library.RemoveShelf("general"));
    }

    [Fact]
    public void Library_GetShelf_Throws_When_Missing()
    {
        var library = new Library();
        Assert.Throws<KeyNotFoundException>(() => library.GetShelf("missing"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Library_Operations_Throw_On_Invalid_Name(string name)
    {
        var library = new Library();
        Assert.Throws<ArgumentException>(() => library.CreateShelf(name));
        Assert.Throws<ArgumentException>(() => library.GetShelf(name));
        Assert.Throws<ArgumentException>(() => library.GetOrCreateShelf(name));
        Assert.Throws<ArgumentException>(() => library.ContainsShelf(name));
        Assert.Throws<ArgumentException>(() => library.RemoveShelf(name));
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
