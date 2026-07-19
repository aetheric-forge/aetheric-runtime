using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Services;
using AethericForge.Runtime.Institutions.PostOffice;
using AethericForge.Runtime.Services.Post;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Post;

public class PostmasterTests
{
    private readonly Mock<IPostService> _serviceMock;
    private readonly Mock<IPostExchange> _exchangeMock;
    private readonly Mock<ITeam<IPostClerk>> _teamMock;
    private readonly Postmaster _postmaster;

    public PostmasterTests(Mock<IPostExchange> exchangeMock)
    {
        _exchangeMock = exchangeMock;
        _serviceMock = new Mock<IPostService>();
        _teamMock = new Mock<ITeam<IPostClerk>>();
        _postmaster = new Postmaster(_teamMock.Object, _serviceMock.Object);
    }

    [Fact]
    public void Constructor_WhenTeamIsNull_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Postmaster(null!, _serviceMock.Object));
    }

    [Fact]
    public void Constructor_WhenExchangeIsNull_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Postmaster(_teamMock.Object, null!));
    }

    [Fact]
    public void Team_ShouldReturnInjectedTeam()
    {
        Assert.NotNull(_teamMock);
        Assert.NotNull(_postmaster);
        Assert.Same(_teamMock.Object, _postmaster.Team);
    }

    [Fact]
    public async Task SendAsync_WhenEnvelopeIsNull_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _postmaster.AcceptAsync(null!));
    }

    [Fact]
    public async Task SendAsync_ShouldDelegateToExchange()
    {
        // Arrange
        var envelopeMock = new Mock<IPostEnvelope>();
        var referenceMock = new Mock<IPostReference>();
        _exchangeMock
            .Setup(x => x.AcceptAsync(envelopeMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referenceMock.Object);

        // Act
        var result = await _postmaster.AcceptAsync(envelopeMock.Object);

        // Assert
        Assert.Equal(referenceMock.Object, result);
        _exchangeMock.Verify(x => x.AcceptAsync(envelopeMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveAsync_WhenReferenceIsNull_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _postmaster.CollectAsync(null!));
    }

    [Fact]
    public async Task ReceiveAsync_ShouldDelegateToExchange()
    {
        // Arrange
        var referenceMock = new Mock<IPostReference>();
        var envelopeMock = new Mock<IPostEnvelope>();
        _exchangeMock
            .Setup(x => x.CollectAsync(referenceMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelopeMock.Object);

        // Act
        var result = await _postmaster.CollectAsync(referenceMock.Object);

        // Assert
        Assert.Equal(envelopeMock.Object, result);
        _exchangeMock.Verify(x => x.CollectAsync(referenceMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        var referenceMock = new Mock<IPostReference>();
        _exchangeMock
            .Setup(x => x.CollectAsync(referenceMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPostEnvelope?)null);

        // Act
        var result = await _postmaster.CollectAsync(referenceMock.Object);

        // Assert
        Assert.Null(result);
    }
}
