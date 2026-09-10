using AethericForge.Runtime.Abstractions.Interfaces.Security.Services;
using AethericForge.Runtime.Institutions.Security;
using Moq;

namespace AethericForge.Runtime.Tests.Security;

public sealed class SecurityTests
{
    private readonly Mock<ISecurityContext> _contextMock = new();
    private readonly Mock<ISentinel> _sentinelMock = new();
    private readonly Institutions.Security.Security _security;

    public SecurityTests()
    {
        _security = new Institutions.Security.Security(_contextMock.Object, _sentinelMock.Object);
    }

    [Fact]
    public void Context_ShouldBeSet()
    {
        Assert.Equal(_contextMock.Object, _security.Context);
    }

    [Fact]
    public void Sentinel_ShouldBeSet()
    {
        Assert.Equal(_sentinelMock.Object, _security.Sentinel);
    }

    [Fact]
    public void Constructor_WithNullSentinel_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new Institutions.Security.Security(_contextMock.Object, null!));
    }
}
