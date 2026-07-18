using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Institutions.PostOffice;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Post;

public class PostOfficeTests
{
    private readonly Mock<IPostOfficeContext> _contextMock;
    private readonly Mock<IPostmaster> _postmasterMock;
    private readonly PostOffice _postOffice;

    public PostOfficeTests()
    {
        _contextMock = new Mock<IPostOfficeContext>();
        _postmasterMock = new Mock<IPostmaster>();
        _postOffice = new PostOffice(_contextMock.Object, _postmasterMock.Object);
    }

    [Fact]
    public void Context_ShouldBeSet()
    {
        Assert.Equal(_contextMock.Object, _postOffice.Context);
    }

    [Fact]
    public void Postmaster_ShouldBeSet()
    {
        Assert.Equal(_postmasterMock.Object, _postOffice.Postmaster);
    }

    [Fact]
    public async Task AcceptAsync_ShouldDelegateToPostmaster()
    {
        // Arrange
        var envelopeMock = new Mock<IPostEnvelope>();
        var referenceMock = new Mock<IPostReference>();
        _postmasterMock
            .Setup(x => x.AcceptAsync(envelopeMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referenceMock.Object);

        // Act
        var result = await _postOffice.Postmaster.AcceptAsync(envelopeMock.Object);

        // Assert
        Assert.Equal(referenceMock.Object, result);
        _postmasterMock.Verify(x => x.CollectAsync(referenceMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CollectAsync_ShouldDelegateToPostmaster()
    {
        // Arrange
        var referenceMock = new Mock<IPostReference>();
        var envelopeMock = new Mock<IPostEnvelope>();
        _postmasterMock
            .Setup(x => x.CollectAsync(referenceMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelopeMock.Object);

        // Act
        var result = await _postOffice.Postmaster.CollectAsync(referenceMock.Object);

        // Assert
        Assert.Equal(envelopeMock.Object, result);
        _postmasterMock.Verify(x => x.CollectAsync(referenceMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }
}
