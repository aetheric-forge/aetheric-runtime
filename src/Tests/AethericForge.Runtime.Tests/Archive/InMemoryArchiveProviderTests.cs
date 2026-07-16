using AethericForge.Runtime.Models.Archive.Primitives;
using AethericForge.Runtime.Providers.Archive.InMemory;
using Xunit;

namespace AethericForge.Runtime.Tests.Archive;

public class InMemoryArchiveProviderTests
{
    private readonly InMemoryArchiveProvider _provider;
    private const string StoreName = "test-store";

    public InMemoryArchiveProviderTests()
    {
        _provider = new InMemoryArchiveProvider(StoreName);
    }

    [Fact]
    public async Task PutAndRetrieve_WorkCorrectly()
    {
        // Arrange
        var key = "test.txt";
        var content = new byte[] { 1, 2, 3, 4 };
        var metadata = new ArchiveMetadata(contentType: "text/plain");

        // Act
        var reference = await _provider.PutAsync(key, new MemoryStream(content), metadata);
        using var retrievedStream = await _provider.RetrieveAsync(reference);
        var retrievedContent = new byte[4];
        await retrievedStream.ReadExactlyAsync(retrievedContent);

        // Assert
        Assert.Equal(StoreName, reference.Store);
        Assert.Equal(key, reference.Key);
        Assert.Equal(content, retrievedContent);
        
        var retrievedMetadata = await _provider.StatAsync(reference);
        Assert.NotNull(retrievedMetadata);
        Assert.Equal("text/plain", retrievedMetadata.ContentType);
        Assert.Equal(4, retrievedMetadata.ContentLength);
    }

    [Fact]
    public async Task ExistsAndDelete_WorkCorrectly()
    {
        // Arrange
        var key = "delete-me";
        var reference = await _provider.PutAsync(key, new MemoryStream());

        // Act & Assert
        Assert.True(await _provider.ExistsAsync(reference));
        
        var deleted = await _provider.DeleteAsync(reference);
        Assert.True(deleted);
        
        Assert.False(await _provider.ExistsAsync(reference));
    }

    [Fact]
    public async Task Retrieve_Throws_WhenNotFound()
    {
        var reference = new ArchiveReference(StoreName, "missing");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _provider.RetrieveAsync(reference));
    }

    [Fact]
    public async Task Operations_Throw_WhenStoreMismatch()
    {
        var reference = new ArchiveReference("other-store", "key");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.ExistsAsync(reference));
    }
}
