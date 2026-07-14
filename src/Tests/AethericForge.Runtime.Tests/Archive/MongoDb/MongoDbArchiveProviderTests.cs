using System.Security.Cryptography;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Models.Archive;
using AethericForge.Runtime.Providers.Archive.MongoDb;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Archive.MongoDb;

public class MongoDbArchiveProviderTests
{
    private readonly Mock<IMongoCollection<MongoDbArchiveProvider.MongoArchiveDocument>> _mockCollection;
    private readonly Mock<IMongoDatabase> _mockDatabase;
    private readonly MongoDbArchiveProvider _provider;
    private const string StoreName = "test-store";

    public MongoDbArchiveProviderTests()
    {
        _mockCollection = new Mock<IMongoCollection<MongoDbArchiveProvider.MongoArchiveDocument>>();
        _mockDatabase = new Mock<IMongoDatabase>();
        
        _mockDatabase
            .Setup(x => x.GetCollection<MongoDbArchiveProvider.MongoArchiveDocument>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(_mockCollection.Object);

        _provider = new MongoDbArchiveProvider(_mockDatabase.Object, StoreName, "test-collection");
    }

    [Fact]
    public void Store_ShouldReturnStoreName()
    {
        Assert.Equal(StoreName, _provider.Store);
    }

    [Fact]
    public async Task PutAsync_ShouldInsertDocument()
    {
        // Arrange
        var key = "test-key";
        var content = "test-content"u8.ToArray();
        using var stream = new MemoryStream(content);
        var ct = CancellationToken.None;

        // Act
        await _provider.PutAsync(key, stream, ct: ct);

        // Assert
        _mockCollection.Verify(x => x.ReplaceOneAsync(
            It.IsAny<FilterDefinition<MongoDbArchiveProvider.MongoArchiveDocument>>(),
            It.Is<MongoDbArchiveProvider.MongoArchiveDocument>(d => d.Key == key && d.Content.SequenceEqual(content)),
            It.IsAny<ReplaceOptions>(),
            ct), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldCallDeleteOne()
    {
        // Arrange
        var key = "test-key";
        var reference = new ArchiveReference(StoreName, key);
        var ct = CancellationToken.None;
        var deleteResult = new Mock<DeleteResult>();
        deleteResult.Setup(x => x.DeletedCount).Returns(1);

        _mockCollection.Setup(x => x.DeleteOneAsync(
            It.IsAny<FilterDefinition<MongoDbArchiveProvider.MongoArchiveDocument>>(),
            ct))
            .ReturnsAsync(deleteResult.Object);

        // Act
        var result = await _provider.DeleteAsync(reference, ct);

        // Assert
        Assert.True(result);
        _mockCollection.Verify(x => x.DeleteOneAsync(It.IsAny<FilterDefinition<MongoDbArchiveProvider.MongoArchiveDocument>>(), ct), Times.Once);
    }
}
