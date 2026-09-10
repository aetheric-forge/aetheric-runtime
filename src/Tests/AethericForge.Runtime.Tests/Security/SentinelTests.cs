using AethericForge.Runtime.Abstractions.Interfaces.Security.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Services;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Services.Security;
using AethericForge.Runtime.Services.Workbench;

namespace AethericForge.Runtime.Tests.Security;

public sealed class SentinelTests
{
    private const string Domain = "test-domain";

    [Fact]
    public async Task RaiseAsync_ThenGetAsync_ReturnsTheSameRecord()
    {
        var sentinel = CreateSentinel();

        var record = await sentinel.RaiseAsync(
            Domain, SecurityRecordKind.Incident, "Suspicious login", "Details here", "brian", SecuritySeverity.High);
        var fetched = await sentinel.GetAsync(Domain, record.Id);

        Assert.Equal(record, fetched);
        Assert.Equal(SecurityStatus.Open, record.Status);
    }

    [Fact]
    public async Task RaiseAsync_AsAuditFinding_IsPreserved()
    {
        var sentinel = CreateSentinel();

        var record = await sentinel.RaiseAsync(
            Domain, SecurityRecordKind.AuditFinding, "Missing MFA", "Details here", "brian", SecuritySeverity.Medium);

        Assert.Equal(SecurityRecordKind.AuditFinding, record.Kind);
    }

    [Fact]
    public async Task RaiseAsync_RecordsACreationEvent()
    {
        var sentinel = CreateSentinel();
        var record = await sentinel.RaiseAsync(
            Domain, SecurityRecordKind.Incident, "Suspicious login", "Details here", "brian", SecuritySeverity.High);

        var history = await sentinel.GetHistoryAsync(Domain, record.Id);

        var creation = Assert.Single(history);
        Assert.Null(creation.PreviousStatus);
        Assert.Equal(SecurityStatus.Open, creation.NewStatus);
        Assert.Equal("brian", creation.ActorId);
    }

    [Fact]
    public async Task UpdateStatusAsync_ThroughMultipleTransitions_AppendsEachToHistory()
    {
        var sentinel = CreateSentinel();
        var record = await sentinel.RaiseAsync(
            Domain, SecurityRecordKind.Incident, "Suspicious login", "Details here", "brian", SecuritySeverity.High);

        await sentinel.UpdateStatusAsync(Domain, record.Id, SecurityStatus.Investigating, "sec-team", "Looking into it.");
        await sentinel.UpdateStatusAsync(Domain, record.Id, SecurityStatus.Resolved, "sec-team", "False alarm.");

        var history = await sentinel.GetHistoryAsync(Domain, record.Id);

        Assert.Equal(3, history.Count);
        Assert.Equal(SecurityStatus.Open, history[0].NewStatus);
        Assert.Equal(SecurityStatus.Open, history[1].PreviousStatus);
        Assert.Equal(SecurityStatus.Investigating, history[1].NewStatus);
        Assert.Equal("Looking into it.", history[1].Note);
        Assert.Equal(SecurityStatus.Investigating, history[2].PreviousStatus);
        Assert.Equal(SecurityStatus.Resolved, history[2].NewStatus);
        Assert.Equal("sec-team", history[2].ActorId);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToResolved_SetsResolvedAtAndNote()
    {
        var sentinel = CreateSentinel();
        var record = await sentinel.RaiseAsync(
            Domain, SecurityRecordKind.Incident, "Suspicious login", "Details here", "brian", SecuritySeverity.High);

        var updated = await sentinel.UpdateStatusAsync(Domain, record.Id, SecurityStatus.Resolved, "sec-team", "Fixed.");

        Assert.NotNull(updated);
        Assert.Equal(SecurityStatus.Resolved, updated!.Status);
        Assert.Equal("Fixed.", updated.ResolutionNote);
        Assert.NotNull(updated.ResolvedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToInvestigating_DoesNotSetResolvedAt()
    {
        var sentinel = CreateSentinel();
        var record = await sentinel.RaiseAsync(
            Domain, SecurityRecordKind.Incident, "Suspicious login", "Details here", "brian", SecuritySeverity.High);

        var updated = await sentinel.UpdateStatusAsync(Domain, record.Id, SecurityStatus.Investigating, "sec-team");

        Assert.NotNull(updated);
        Assert.Equal(SecurityStatus.Investigating, updated!.Status);
        Assert.Null(updated.ResolvedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_ForAnUnknownId_ReturnsNull()
    {
        var sentinel = CreateSentinel();

        var updated = await sentinel.UpdateStatusAsync(Domain, Guid.NewGuid(), SecurityStatus.Investigating, "sec-team");

        Assert.Null(updated);
    }

    [Fact]
    public async Task GetHistoryAsync_IsScopedToTheRequestedRecord()
    {
        var sentinel = CreateSentinel();
        var first = await sentinel.RaiseAsync(
            Domain, SecurityRecordKind.Incident, "Incident A", "Details", "brian", SecuritySeverity.Low);
        var second = await sentinel.RaiseAsync(
            Domain, SecurityRecordKind.Incident, "Incident B", "Details", "brian", SecuritySeverity.Low);
        await sentinel.UpdateStatusAsync(Domain, first.Id, SecurityStatus.Investigating, "sec-team");

        var firstHistory = await sentinel.GetHistoryAsync(Domain, first.Id);
        var secondHistory = await sentinel.GetHistoryAsync(Domain, second.Id);

        Assert.Equal(2, firstHistory.Count);
        Assert.Single(secondHistory);
    }

    [Fact]
    public async Task ListAsync_IsScopedPerDomain()
    {
        var sentinel = CreateSentinel();
        await sentinel.RaiseAsync("domain-a", SecurityRecordKind.Incident, "Incident A", "Details", "brian", SecuritySeverity.Low);
        await sentinel.RaiseAsync("domain-b", SecurityRecordKind.Incident, "Incident B", "Details", "brian", SecuritySeverity.Low);

        var domainARecords = await sentinel.ListAsync("domain-a");
        var domainBRecords = await sentinel.ListAsync("domain-b");

        Assert.Single(domainARecords);
        Assert.Single(domainBRecords);
        Assert.NotEqual(domainARecords[0].Id, domainBRecords[0].Id);
    }

    [Fact]
    public async Task GetHistoryAsync_ForAnUnknownDomain_ReturnsEmpty()
    {
        var sentinel = CreateSentinel();

        var history = await sentinel.GetHistoryAsync("never-posted-to", Guid.NewGuid());

        Assert.Empty(history);
    }

    private static Sentinel CreateSentinel()
    {
        var workbenchService = new WorkbenchService();
        return new Sentinel(workbenchService, new Team<ISecurityClerk>([]));
    }
}
