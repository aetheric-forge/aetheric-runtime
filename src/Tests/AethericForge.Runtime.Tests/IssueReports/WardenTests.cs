using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Services;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Services.IssueReports;
using AethericForge.Runtime.Services.Workbench;

namespace AethericForge.Runtime.Tests.IssueReports;

public sealed class WardenTests
{
    private const string Domain = "test-domain";

    [Fact]
    public async Task SubmitAsync_ThenGetAsync_ReturnsTheSameReport()
    {
        var warden = CreateWarden();

        var report = await warden.SubmitAsync(Domain, "Login fails", "Details here", "brian");
        var fetched = await warden.GetAsync(Domain, report.Id);

        Assert.Equal(report, fetched);
        Assert.Equal(IssueStatus.Open, report.Status);
    }

    [Fact]
    public async Task SubmitAsync_ThenListAsync_IncludesTheNewReport()
    {
        var warden = CreateWarden();
        var report = await warden.SubmitAsync(Domain, "Login fails", "Details here", "brian");

        var reports = await warden.ListAsync(Domain);

        var listed = Assert.Single(reports);
        Assert.Equal(report, listed);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToResolved_SetsResolvedAtAndNote()
    {
        var warden = CreateWarden();
        var report = await warden.SubmitAsync(Domain, "Login fails", "Details here", "brian");

        var updated = await warden.UpdateStatusAsync(Domain, report.Id, IssueStatus.Resolved, "Fixed in PAR change.");

        Assert.NotNull(updated);
        Assert.Equal(IssueStatus.Resolved, updated!.Status);
        Assert.Equal("Fixed in PAR change.", updated.ResolutionNote);
        Assert.NotNull(updated.ResolvedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_ToInProgress_DoesNotSetResolvedAt()
    {
        var warden = CreateWarden();
        var report = await warden.SubmitAsync(Domain, "Login fails", "Details here", "brian");

        var updated = await warden.UpdateStatusAsync(Domain, report.Id, IssueStatus.InProgress);

        Assert.NotNull(updated);
        Assert.Equal(IssueStatus.InProgress, updated!.Status);
        Assert.Null(updated.ResolvedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_ForAnUnknownId_ReturnsNull()
    {
        var warden = CreateWarden();

        var updated = await warden.UpdateStatusAsync(Domain, Guid.NewGuid(), IssueStatus.InProgress);

        Assert.Null(updated);
    }

    [Fact]
    public async Task ListAsync_IsScopedPerDomain()
    {
        var warden = CreateWarden();
        await warden.SubmitAsync("domain-a", "Issue A", "Details", "brian");
        await warden.SubmitAsync("domain-b", "Issue B", "Details", "brian");

        var domainAReports = await warden.ListAsync("domain-a");
        var domainBReports = await warden.ListAsync("domain-b");

        Assert.Single(domainAReports);
        Assert.Single(domainBReports);
        Assert.NotEqual(domainAReports[0].Id, domainBReports[0].Id);
    }

    [Fact]
    public async Task ListAsync_ForAnUnknownDomain_ReturnsEmpty()
    {
        var warden = CreateWarden();

        var reports = await warden.ListAsync("never-posted-to");

        Assert.Empty(reports);
    }

    private static Warden CreateWarden()
    {
        var workbenchService = new WorkbenchService();
        return new Warden(workbenchService, new Team<IIssueReportsClerk>([]));
    }
}
