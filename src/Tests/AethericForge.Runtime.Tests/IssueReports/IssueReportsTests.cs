using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Services;
using AethericForge.Runtime.Institutions.IssueReports;
using Moq;

namespace AethericForge.Runtime.Tests.IssueReports;

public sealed class IssueReportsTests
{
    private readonly Mock<IIssueReportsContext> _contextMock = new();
    private readonly Mock<IWarden> _wardenMock = new();
    private readonly Institutions.IssueReports.IssueReports _issueReports;

    public IssueReportsTests()
    {
        _issueReports = new Institutions.IssueReports.IssueReports(_contextMock.Object, _wardenMock.Object);
    }

    [Fact]
    public void Context_ShouldBeSet()
    {
        Assert.Equal(_contextMock.Object, _issueReports.Context);
    }

    [Fact]
    public void Warden_ShouldBeSet()
    {
        Assert.Equal(_wardenMock.Object, _issueReports.Warden);
    }

    [Fact]
    public void Constructor_WithNullWarden_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new Institutions.IssueReports.IssueReports(_contextMock.Object, null!));
    }
}
