using AethericForge.Runtime.Models.Staging;
using AethericForge.Runtime.Providers.Staging.InMemory;
using Xunit;

namespace AethericForge.Runtime.Tests.Staging;

public class InMemoryStagingProviderTests
{
    private readonly InMemoryStagingProvider _provider;
    private const string Stage = "test-stage";

    public InMemoryStagingProviderTests()
    {
        _provider = new InMemoryStagingProvider(Stage);
    }

    [Fact]
    public async Task PutAndOpenRead_WorkCorrectly()
    {
        // Arrange
        var key = "test.data";
        var content = new byte[] { 10, 20, 30 };
        var metadata = new StagingMetadata("application/octet-stream");

        // Act
        var reference = await _provider.PutAsync(key, new MemoryStream(content), metadata);
        using var retrievedStream = await _provider.OpenReadAsync(reference);
        var retrievedContent = new byte[3];
        await retrievedStream.ReadExactlyAsync(retrievedContent);

        // Assert
        Assert.Equal(content, retrievedContent);
    }

    [Fact]
    public async Task Pinning_Prevents_Deletion()
    {
        // Arrange
        var key = "pinned.data";
        var reference = await _provider.PutAsync(key, new MemoryStream());

        // Act
        await _provider.PinAsync(reference);
        var deleted = await _provider.DeleteAsync(reference);

        // Assert
        Assert.False(deleted);
        Assert.True(await _provider.ExistsAsync(reference));

        // Act - Unpin
        await _provider.UnpinAsync(reference);
        deleted = await _provider.DeleteAsync(reference);

        // Assert
        Assert.True(deleted);
        Assert.False(await _provider.ExistsAsync(reference));
    }

    [Fact]
    public async Task Lock_Acquisition_Works()
    {
        // Arrange
        var key = "locked.data";
        var reference = new StagingReference(Stage, key);

        // Act
        await using var stagingLock = await _provider.AcquireLockAsync(reference);

        // Assert
        Assert.True(stagingLock.IsAcquired);
        Assert.Equal(reference.Key, stagingLock.Reference.Key);
    }
}
