using AethericForge.Runtime.Abstractions.Interfaces.Archive.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Models.Archive.Primitives;
using AethericForge.Runtime.Providers.Archive.InMemory;
using AethericForge.Runtime.Services.Archive;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Archive;

public class ArchiveServiceTests
{
    private readonly IArchiveProvider _provider;
    private readonly ArchiveService _service;
    private const string StoreName = "test-store";

    public ArchiveServiceTests()
    {
        _provider = new InMemoryArchiveProvider(StoreName);
        _service = new ArchiveService(new[] { _provider });
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
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        var result = await _service.PutAsync(StoreName, key, stream);

        // Assert
        Assert.Equal(StoreName, result.Store);
        Assert.Equal(key, result.Key);
        
        using var retrieved = await _provider.RetrieveAsync(result);
        var bytes = new byte[3];
        await retrieved.ReadExactlyAsync(bytes);
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
    }

    [Fact]
    public async Task RetrieveAsync_Calls_Correct_Provider()
    {
        // Arrange
        var key = "key";
        var content = new byte[] { 4, 5, 6 };
        await _provider.PutAsync(key, new MemoryStream(content));
        var reference = new ArchiveReference(StoreName, key);

        // Act
        using var result = await _service.RetrieveAsync(reference);

        // Assert
        var bytes = new byte[3];
        await result.ReadExactlyAsync(bytes);
        Assert.Equal(content, bytes);
    }

    [Fact]
    public async Task ArchiveAsync_Calls_Default_Provider()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 7, 8, 9 });

        // Act
        var result = await _service.ArchiveAsync(stream);

        // Assert
        Assert.Equal(StoreName, result.Store);
        Assert.NotEmpty(result.Key);
        
        Assert.True(await _provider.ExistsAsync(result));
    }

    [Fact]
    public async Task StatAsync_Calls_Correct_Provider()
    {
        // Arrange
        var key = "key";
        var metadata = new ArchiveMetadata(contentType: "text/plain");
        await _provider.PutAsync(key, new MemoryStream(), metadata);
        var reference = new ArchiveReference(StoreName, key);

        // Act
        var result = await _service.StatAsync(reference);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("text/plain", result.ContentType);
    }

    [Fact]
    public async Task ExistsAsync_Calls_Correct_Provider()
    {
        // Arrange
        var key = "key";
        await _provider.PutAsync(key, new MemoryStream());
        var reference = new ArchiveReference(StoreName, key);

        // Act
        var result = await _service.ExistsAsync(reference);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteAsync_Calls_Correct_Provider()
    {
        // Arrange
        var key = "key";
        await _provider.PutAsync(key, new MemoryStream());
        var reference = new ArchiveReference(StoreName, key);

        // Act
        var result = await _service.DeleteAsync(reference);

        // Assert
        Assert.True(result);
        Assert.False(await _provider.ExistsAsync(reference));
    }

    [Fact]
    public async Task PutAsync_Throws_When_Store_Not_Found()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _service.PutAsync("unknown", "key", new MemoryStream()));
    }
}
