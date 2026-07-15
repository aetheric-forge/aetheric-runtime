using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Serialization;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Models.Archive.Primitives;
using AethericForge.Runtime.Models.Archive.Serialization;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Services.Archive;
using Moq;

namespace AethericForge.Runtime.Tests.Archive;

public class ArchivistTests
{
    private readonly Mock<IArchiveService> _archiveServiceMock;
    private readonly JsonArchiveSerializer _jsonSerializer;
    private readonly Archivist _archivist;

    public ArchivistTests()
    {
        _archiveServiceMock = new Mock<IArchiveService>();
        _jsonSerializer = new JsonArchiveSerializer();
        _archivist = new Archivist(_archiveServiceMock.Object, new Team<IArchiveClerk>(Array.Empty<IArchiveClerk>()),new[] { _jsonSerializer });
    }

    [Fact]
    public async Task PutAsync_ShouldSerializeAndStoreObject()
    {
        // Arrange
        var store = "test-store";
        var key = "test-key";
        var value = new TestObject { Name = "Test", Value = 123 };
        var reference = new ArchiveReference(store, key);

        _archiveServiceMock
            .Setup(x => x.PutAsync(
                store,
                key,
                It.IsAny<Stream>(),
                It.IsAny<IArchiveMetadata>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        // Act
        var result = await _archivist.PutAsync(store, key, value);

        // Assert
        Assert.Equal(reference, result);
        _archiveServiceMock.Verify(x => x.PutAsync(
            store,
            key,
            It.IsAny<Stream>(),
            It.Is<IArchiveMetadata>(m => m.ContentType == "application/json"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldRetrieveAndDeserializeObject()
    {
        // Arrange
        var store = "test-store";
        var key = "test-key";
        var value = new TestObject { Name = "Test", Value = 123 };
        var reference = new ArchiveReference(store, key);
        var metadata = new ArchiveMetadata(contentType: "application/json");

        using var stream = new MemoryStream();
        await _jsonSerializer.SerializeAsync(stream, value);
        stream.Position = 0;

        _archiveServiceMock
            .Setup(x => x.StatAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        _archiveServiceMock
            .Setup(x => x.RetrieveAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        // Act
        var result = await _archivist.GetAsync<TestObject>(reference);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(value.Name, result.Name);
        Assert.Equal(value.Value, result.Value);
    }

    [Fact]
    public async Task GetAsync_WhenMetadataMissingContentType_ShouldReturnDefault()
    {
        // Arrange
        var reference = new ArchiveReference("store", "key");
        var metadata = new ArchiveMetadata(contentType: null);

        _archiveServiceMock
            .Setup(x => x.StatAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        // Act
        var result = await _archivist.GetAsync<TestObject>(reference);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WhenNoSerializerForContentType_ShouldReturnDefault()
    {
        // Arrange
        var reference = new ArchiveReference("store", "key");
        var metadata = new ArchiveMetadata(contentType: "application/xml");

        _archiveServiceMock
            .Setup(x => x.StatAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        // Act
        var result = await _archivist.GetAsync<TestObject>(reference);

        // Assert
        Assert.Null(result);
    }

    private class TestObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
