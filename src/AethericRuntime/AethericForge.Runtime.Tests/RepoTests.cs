
using AethericForge.Runtime.Repo.Abstractions;

namespace AethericForge.Runtime.Tests;

public class RepoTests
{
    public static IEnumerable<object[]> Cases => TestMatrix.RepoCases();

    // --- core semantics -----------------------------------------------------

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Upsert_Then_Get_Returns_Deep_Copy(Func<IRepo<TestModels.TestMessage>> factory)
    {
        var repo = factory();
        await repo.ClearAsync();

        var message = new TestModels.TestMessage("test-message-1");

        message = await repo.UpsertAsync(message);

        var fetched = await repo.GetAsync(message.Id);

        Assert.NotSame(message, fetched);      // stored copy ≠ original
        Assert.Equal(message.Id, fetched?.Id);
        Assert.Equal(message.Message, fetched?.Message);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Upsert_WithSameId_Replaces_Existing_Item(Func<IRepo<TestModels.TestMessage>> factory)
    {
        var repo = factory();
        await repo.ClearAsync();

        var message = new TestModels.TestMessage("original message");
        message = await repo.UpsertAsync(message);
        var updated = new TestModels.TestMessage(message.Id, "updated message");
        await repo.UpsertAsync(updated);

        var fetched = await repo.GetAsync(message.Id);

        Assert.Equal("updated message", fetched?.Message);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Delete_Removes_Item_And_Returns_True_When_Deleted(Func<IRepo<TestModels.TestMessage>> factory)
    {
        var repo = factory();
        await repo.ClearAsync();
        var message = new TestModels.TestMessage("delete me");

        message = await repo.UpsertAsync(message);

        var deleted = await repo.DeleteAsync(message.Id);

        Assert.NotNull(deleted);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.GetAsync(message.Id));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Clear_Removes_All_Items(Func<IRepo<TestModels.TestMessage>> factory)
    {
        var repo = factory();
        await repo.ClearAsync();

        var all = await repo.ListAsync(new FilterSpec());
        Assert.Empty(all);
    }

    // --- List + FilterSpec semantics ---------------------------------------

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task List_With_Empty_Filter_Returns_All_Items(Func<IRepo<TestModels.TestMessage>> factory)
    {
        var repo = factory();
        await repo.ClearAsync();

        var messages = new List<TestModels.TestMessage>([
            new TestModels.TestMessage("message-1"),
            new TestModels.TestMessage("message-2"),
            new TestModels.TestMessage("message-3"),
        ]);

        foreach (var message in messages) {
            await repo.UpsertAsync(message);
        }

        var all = await repo.ListAsync(new FilterSpec());

        Assert.Equal(3, all.Count);
        Assert.Contains(all, t => t.Message == "message-1");
        Assert.Contains(all, t => t.Message == "message-2");
        Assert.Contains(all, t => t.Message == "message-3");
    }
}
