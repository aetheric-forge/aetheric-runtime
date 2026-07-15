using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Institutions.PostOffice;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Post;

public class PostOfficeTests
{
    private readonly Mock<IPostOfficeContext> _contextMock;
    private readonly Mock<IPostExchange> _exchangeMock;
    private readonly Mock<IPostmaster> _postmasterMock;
    private readonly PostOffice _postOffice;

    public PostOfficeTests()
    {
        _contextMock = new Mock<IPostOfficeContext>();
        _exchangeMock = new Mock<IPostExchange>();
        _postmasterMock = new Mock<IPostmaster>();
        _postOffice = new PostOffice(_contextMock.Object, _exchangeMock.Object, _postmasterMock.Object);
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
    public async Task AcceptAsync_ShouldDelegateToExchange()
    {
        // Arrange
        var envelopeMock = new Mock<IPostEnvelope>();
        var referenceMock = new Mock<IPostReference>();
        _exchangeMock
            .Setup(x => x.AcceptAsync(envelopeMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referenceMock.Object);

        // Act
        var result = await _postOffice.AcceptAsync(envelopeMock.Object);

        // Assert
        Assert.Equal(referenceMock.Object, result);
        _exchangeMock.Verify(x => x.AcceptAsync(envelopeMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CollectAsync_ShouldDelegateToExchange()
    {
        // Arrange
        var referenceMock = new Mock<IPostReference>();
        var envelopeMock = new Mock<IPostEnvelope>();
        _exchangeMock
            .Setup(x => x.CollectAsync(referenceMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelopeMock.Object);

        // Act
        var result = await _postOffice.CollectAsync(referenceMock.Object);

        // Assert
        Assert.Equal(envelopeMock.Object, result);
        _exchangeMock.Verify(x => x.CollectAsync(referenceMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }
}
