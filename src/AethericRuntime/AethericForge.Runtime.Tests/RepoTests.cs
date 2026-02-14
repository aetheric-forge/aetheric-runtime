
using AethericForge.Runtime.Repo.Abstractions;

namespace AethericForge.Runtime.Tests;

public class RepoTests
{
    public static IEnumerable<object[]> Cases => TestMatrix.RepoCases();

    // --- core semantics -----------------------------------------------------

    [Theory]
    [MemberData(nameof(Cases))]
    public void Upsert_Then_Get_Returns_Deep_Copy(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();
        var saga = TestModels.Saga(id: "saga-1");

        repo.Upsert(saga);

        var fetched1 = repo.Get("saga-1");
        var fetched2 = repo.Get("saga-1");

        Assert.NotSame(saga, fetched1);      // stored copy ≠ original
        Assert.NotSame(fetched1, fetched2);  // each Get() returns a fresh copy

        Assert.Equal(saga.Id, fetched1.Id);
        Assert.Equal(saga.Title, fetched1.Title);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Upsert_WithSameId_Replaces_Existing_Item(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();

        var saga1 = TestModels.Saga(id: "saga-1", title: "Parallel You v0.3");
        var saga2 = TestModels.Saga(id: "saga-1", title: "Parallel You v0.3.3");

        repo.Upsert(saga1);
        repo.Upsert(saga2); // same id, should overwrite

        var fetched = repo.Get("saga-1");

        Assert.Equal("Parallel You v0.3.3", fetched.Title);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Delete_Removes_Item_And_Returns_True_When_Deleted(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();
        var saga = TestModels.Saga(id: "saga-1");

        repo.Upsert(saga);

        var deleted = repo.Delete("saga-1");

        Assert.True(deleted);
        Assert.Throws<KeyNotFoundException>(() => repo.Get("saga-1"));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Delete_Returns_False_When_Item_Does_Not_Exist(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();

        var deleted = repo.Delete("missing-id");

        Assert.False(deleted);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Clear_Removes_All_Items(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();

        repo.Upsert(TestModels.Domain(id: "domain-1"));
        repo.Upsert(TestModels.Saga(id: "saga-1"));
        repo.Upsert(TestModels.Story(id: "story-1"));

        repo.Clear();

        var all = repo.List(new FilterSpec());
        Assert.Empty(all);
    }

    // --- List + FilterSpec semantics ---------------------------------------

    [Theory]
    [MemberData(nameof(Cases))]
    public void List_With_Empty_Filter_Returns_All_Items(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();

        repo.Upsert(TestModels.Domain(id: "domain-1"));
        repo.Upsert(TestModels.Saga(id: "saga-1"));
        repo.Upsert(TestModels.Story(id: "story-1"));

        var all = repo.List(new FilterSpec());

        Assert.Equal(3, all.Count);
        Assert.Contains(all, t => t.Id == "domain-1");
        Assert.Contains(all, t => t.Id == "saga-1");
        Assert.Contains(all, t => t.Id == "story-1");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void List_Text_Filter_Matches_Title_Case_Insensitive(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();

        repo.Upsert(TestModels.Domain(id: "domain-1", title: "Aetheric Forge"));
        repo.Upsert(TestModels.Domain(id: "domain-2", title: "Baator World"));
        repo.Upsert(TestModels.Saga(id: "saga-1", title: "Parallel You"));

        var filter = new FilterSpec(text: "forge");
        var result = repo.List(filter);

        Assert.Single(result);
        Assert.Equal("domain-1", result[0].Id);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void List_Archived_Filter_Respects_Archived_Flag(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();

        repo.Upsert(TestModels.Saga(id: "saga-open", archived: false));
        repo.Upsert(TestModels.Saga(id: "saga-archived", archived: true));

        var onlyActive = repo.List(new FilterSpec(archived: false));
        var onlyArchived = repo.List(new FilterSpec(archived: true));

        Assert.Single(onlyActive);
        Assert.Equal("saga-open", onlyActive[0].Id);

        Assert.Single(onlyArchived);
        Assert.Equal("saga-archived", onlyArchived[0].Id);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void List_Type_Filter_Filters_By_Concrete_Type(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();

        repo.Upsert(TestModels.Domain(id: "domain-1"));
        repo.Upsert(TestModels.Saga(id: "saga-1"));
        repo.Upsert(TestModels.Story(id: "story-1"));

        var filter = new FilterSpec(
            types: new List<Type> { typeof(AethericForge.Runtime.Model.Threads.Saga) }
        );

        var result = repo.List(filter);

        Assert.Single(result);
        Assert.IsType<AethericForge.Runtime.Model.Threads.Saga>(result[0]);
        Assert.Equal("saga-1", result[0].Id);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void List_Can_Combine_Text_Archived_And_Type_Filters(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();

        repo.Upsert(TestModels.Saga(id: "saga-1", title: "Parallel You v0.3", archived: false));
        repo.Upsert(TestModels.Saga(id: "saga-2", title: "Parallel You v0.3.3", archived: true));
        repo.Upsert(TestModels.Saga(id: "saga-3", title: "Other Work", archived: true));

        var filter = new FilterSpec(
            text: "parallel",
            archived: true,
            types: new List<Type> { typeof(AethericForge.Runtime.Model.Threads.Saga) }
        );

        var result = repo.List(filter);

        Assert.Single(result);
        Assert.Equal("saga-2", result[0].Id);
    }

    // --- deep copy behaviour on List ---------------------------------------

    [Theory]
    [MemberData(nameof(Cases))]
    public void List_Returns_Deep_Copies_Not_Internal_References(Func<IRepo> factory)
    {
        var repo = factory();
        repo.Clear();

        repo.Upsert(TestModels.Saga(id: "saga-1", title: "Parallel You"));
        repo.Upsert(TestModels.Saga(id: "saga-2", title: "Parallel You 2"));

        var firstCall = repo.List(new FilterSpec());
        var secondCall = repo.List(new FilterSpec());

        Assert.Equal(2, firstCall.Count);
        Assert.Equal(2, secondCall.Count);

        // same IDs, different references
        for (int i = 0; i < firstCall.Count; i++)
        {
            var a = firstCall[i];
            var b = secondCall.Single(t => t.Id == a.Id);

            Assert.NotSame(a, b);
            Assert.Equal(a.Id, b.Id);
            Assert.Equal(a.Title, b.Title);
        }
    }
}
