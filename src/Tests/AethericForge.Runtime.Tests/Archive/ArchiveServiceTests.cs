using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Models.Archive.Primitives;
using AethericForge.Runtime.Services.Archive;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Archive;

public class ArchiveServiceTests
{
    private readonly Mock<IArchiveProvider> _providerMock;
    private readonly ArchiveService _service;
    private const string StoreName = "test-store";

    public ArchiveServiceTests()
    {
        _providerMock = new Mock<IArchiveProvider>();
        _providerMock.SetupGet(x => x.Store).Returns(StoreName);
        _service = new ArchiveService(new[] { _providerMock.Object });
    }

    [Fact]
    public void Constructor_Throws_When_Providers_Empty()
    {
        Assert.Throws<ArgumentException>(() => new ArchiveService(Enumerable.Empty<IArchiveProvider>()));
    }

    [Fact]
    public async Task PutAsync_Calls_Correct_Provider()
    {
        // Arrange
        var key = "test-key";
        using var stream = new MemoryStream();
        var reference = new ArchiveReference(StoreName, key);
        _providerMock.Setup(x => x.PutAsync(key, stream, It.IsAny<IArchiveMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.PutAsync(StoreName, key, stream);

        // Assert
        Assert.Same(reference, result);
        _providerMock.Verify(x => x.PutAsync(key, stream, It.IsAny<IArchiveMetadata>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_Calls_Correct_Provider()
    {
        // Arrange
        var reference = new ArchiveReference(StoreName, "key");
        using var stream = new MemoryStream();
        _providerMock.Setup(x => x.RetrieveAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        // Act
        var result = await _service.RetrieveAsync(reference);

        // Assert
        Assert.Same(stream, result);
    }

    [Fact]
    public async Task ArchiveAsync_Calls_Default_Provider()
    {
        // Arrange
        using var stream = new MemoryStream();
        var reference = new ArchiveReference(StoreName, "key");
        _providerMock.Setup(x => x.ArchiveAsync(stream, It.IsAny<IArchiveMetadata>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _service.ArchiveAsync(stream);

        // Assert
        Assert.Same(reference, result);
        _providerMock.Verify(x => x.ArchiveAsync(stream, It.IsAny<IArchiveMetadata>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StatAsync_Calls_Correct_Provider()
    {
        // Arrange
        var reference = new ArchiveReference(StoreName, "key");
        var metadata = new ArchiveMetadata();
        _providerMock.Setup(x => x.StatAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        // Act
        var result = await _service.StatAsync(reference);

        // Assert
        Assert.Same(metadata, result);
    }

    [Fact]
    public async Task ExistsAsync_Calls_Correct_Provider()
    {
        // Arrange
        var reference = new ArchiveReference(StoreName, "key");
        _providerMock.Setup(x => x.ExistsAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ExistsAsync(reference);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteAsync_Calls_Correct_Provider()
    {
        // Arrange
        var reference = new ArchiveReference(StoreName, "key");
        _providerMock.Setup(x => x.DeleteAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteAsync(reference);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task PutAsync_Throws_When_Store_Not_Found()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _service.PutAsync("unknown", "key", new MemoryStream()));
    }
}
