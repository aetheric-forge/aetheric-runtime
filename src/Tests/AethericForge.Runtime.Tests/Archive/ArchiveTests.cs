using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Institutions.Archive;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Archive;

public class ArchiveTests
{
    private readonly Mock<IArchiveContext> _contextMock;
    private readonly Mock<IArchiveVault> _vaultMock;
    private readonly Mock<IArchivist> _archivistMock;
    private readonly Institutions.Archive.Archive _archive;

    public ArchiveTests()
    {
        _contextMock = new Mock<IArchiveContext>();
        _vaultMock = new Mock<IArchiveVault>();
        _archivistMock = new Mock<IArchivist>();
        _archive = new Institutions.Archive.Archive(_contextMock.Object, _vaultMock.Object, _archivistMock.Object);
    }

    [Fact]
    public void Context_ShouldBeSet()
    {
        Assert.Equal(_contextMock.Object, _archive.Context);
    }

    [Fact]
    public void Archivist_ShouldBeSet()
    {
        Assert.Equal(_archivistMock.Object, _archive.Archivist);
    }

    [Fact]
    public async Task ArchiveAsync_ShouldDelegateToVault()
    {
        // Arrange
        var content = new MemoryStream();
        var metadataMock = new Mock<IArchiveMetadata>();
        var referenceMock = new Mock<IArchiveReference>();
        _vaultMock
            .Setup(x => x.ArchiveAsync(content, metadataMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referenceMock.Object);

        // Act
        var result = await _archive.ArchiveAsync(content, metadataMock.Object);

        // Assert
        Assert.Equal(referenceMock.Object, result);
        _vaultMock.Verify(x => x.ArchiveAsync(content, metadataMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldDelegateToVault()
    {
        // Arrange
        var referenceMock = new Mock<IArchiveReference>();
        var content = new MemoryStream();
        _vaultMock
            .Setup(x => x.RetrieveAsync(referenceMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        var result = await _archive.RetrieveAsync(referenceMock.Object);

        // Assert
        Assert.Equal(content, result);
        _vaultMock.Verify(x => x.RetrieveAsync(referenceMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StatAsync_ShouldDelegateToVault()
    {
        // Arrange
        var referenceMock = new Mock<IArchiveReference>();
        var metadataMock = new Mock<IArchiveMetadata>();
        _vaultMock
            .Setup(x => x.StatAsync(referenceMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadataMock.Object);

        // Act
        var result = await _archive.StatAsync(referenceMock.Object);

        // Assert
        Assert.Equal(metadataMock.Object, result);
        _vaultMock.Verify(x => x.StatAsync(referenceMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ShouldDelegateToVault()
    {
        // Arrange
        var referenceMock = new Mock<IArchiveReference>();
        _vaultMock
            .Setup(x => x.ExistsAsync(referenceMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _archive.ExistsAsync(referenceMock.Object);

        // Assert
        Assert.True(result);
        _vaultMock.Verify(x => x.ExistsAsync(referenceMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDelegateToVault()
    {
        // Arrange
        var referenceMock = new Mock<IArchiveReference>();
        _vaultMock
            .Setup(x => x.DeleteAsync(referenceMock.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _archive.DeleteAsync(referenceMock.Object);

        // Assert
        Assert.True(result);
        _vaultMock.Verify(x => x.DeleteAsync(referenceMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }
}
