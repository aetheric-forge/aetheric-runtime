using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Services;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Services.Maintenance;
using AethericForge.Runtime.Services.Workbench;

namespace AethericForge.Runtime.Tests.Maintenance;

public sealed class CaretakerTests
{
    private const string Domain = "test-domain";

    [Fact]
    public async Task PostAsync_ThenCollectNextAsync_ReturnsTheSameCommand()
    {
        var caretaker = CreateCaretaker();
        var command = NewCommand("purge");

        var posted = await caretaker.PostAsync(Domain, command);
        var collected = await caretaker.CollectNextAsync(Domain, "purge");

        Assert.Equal(MaintenancePostStatus.Accepted, posted.Status);
        Assert.Equal(command, collected);
    }

    [Fact]
    public async Task PostAsync_WithAlreadyKnownCommandId_ReturnsAlreadyAccepted()
    {
        var caretaker = CreateCaretaker();
        var command = NewCommand("purge");
        await caretaker.PostAsync(Domain, command);

        var result = await caretaker.PostAsync(Domain, command with { Source = "retry" });

        Assert.Equal(MaintenancePostStatus.AlreadyAccepted, result.Status);
        Assert.Equal(command.Source, result.Command.Source);
    }

    [Fact]
    public async Task CollectNextAsync_OnlyReturnsUncollectedCommandsForTheRequestedJob()
    {
        var caretaker = CreateCaretaker();
        var purge = NewCommand("purge");
        var reindex = NewCommand("reindex");
        await caretaker.PostAsync(Domain, purge);
        await caretaker.PostAsync(Domain, reindex);

        var firstPurgeCollect = await caretaker.CollectNextAsync(Domain, "purge");
        var secondPurgeCollect = await caretaker.CollectNextAsync(Domain, "purge");
        var reindexCollect = await caretaker.CollectNextAsync(Domain, "reindex");

        Assert.Equal(purge, firstPurgeCollect);
        Assert.Null(secondPurgeCollect);
        Assert.Equal(reindex, reindexCollect);
    }

    [Fact]
    public async Task RecordOutcomeAsync_AttachesTheOutcomeToItsCommandsRun()
    {
        var caretaker = CreateCaretaker();
        var command = NewCommand("purge");
        await caretaker.PostAsync(Domain, command);
        await caretaker.CollectNextAsync(Domain, "purge");
        var outcome = new MaintenanceRunOutcome(
            command.Id,
            MaintenanceRunStatus.Completed,
            ProcessedCount: 4,
            RemainingCount: 0,
            OccurredAtUtc: DateTimeOffset.UtcNow);

        await caretaker.RecordOutcomeAsync(Domain, outcome);
        var runs = await caretaker.ListRunsAsync(Domain);

        var run = Assert.Single(runs);
        Assert.Equal(command, run.Command);
        Assert.True(run.IsCollected);
        Assert.Equal(outcome, run.Outcome);
    }

    [Fact]
    public async Task ListRunsAsync_IsScopedPerDomain()
    {
        var caretaker = CreateCaretaker();
        await caretaker.PostAsync("domain-a", NewCommand("purge"));
        await caretaker.PostAsync("domain-b", NewCommand("purge"));

        var domainARuns = await caretaker.ListRunsAsync("domain-a");
        var domainBRuns = await caretaker.ListRunsAsync("domain-b");

        Assert.Single(domainARuns);
        Assert.Single(domainBRuns);
        Assert.NotEqual(domainARuns[0].Command.Id, domainBRuns[0].Command.Id);
    }

    [Fact]
    public async Task ListRunsAsync_ForAnUnknownDomain_ReturnsEmpty()
    {
        var caretaker = CreateCaretaker();

        var runs = await caretaker.ListRunsAsync("never-posted-to");

        Assert.Empty(runs);
    }

    private static MaintenanceCommand NewCommand(string job) => new(
        Guid.NewGuid(),
        Domain,
        job,
        DateTimeOffset.UtcNow,
        Source: "test");

    private static Caretaker CreateCaretaker()
    {
        var workbenchService = new WorkbenchService();
        return new Caretaker(workbenchService, new Team<IMaintenanceClerk>([]));
    }
}
