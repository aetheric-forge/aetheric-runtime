using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;
using AethericForge.Runtime.Models.Staging;
using AethericForge.Runtime.Providers.Staging.Redis;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace AethericForge.Runtime.Tests.Staging.Redis;

public class RedisStagingProviderTests
{
    private readonly Mock<IConnectionMultiplexer> _mockMultiplexer;
    private readonly Mock<IDatabase> _mockDb;
    private readonly RedisStagingProvider _provider;
    private const string StageName = "test-stage";

    public RedisStagingProviderTests()
    {
        _mockMultiplexer = new Mock<IConnectionMultiplexer>();
        _mockDb = new Mock<IDatabase>();
        _mockMultiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDb.Object);
        _provider = new RedisStagingProvider(_mockMultiplexer.Object, StageName);
    }

    [Fact]
    public void Stage_ShouldReturnStageName()
    {
        Assert.Equal(StageName, _provider.Stage);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        // Arrange
        var key = "test-key";
        var reference = new StagingReference(StageName, key);
        var redisKey = (RedisKey)$"{StageName}:data:{key}";
        
        _mockDb.Setup(x => x.KeyExistsAsync(redisKey, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        var result = await _provider.ExistsAsync(reference);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenKeyDeleted()
    {
        // Arrange
        var key = "test-key";
        var reference = new StagingReference(StageName, key);
        var redisKey = (RedisKey)$"{StageName}:data:{key}";
        
        _mockDb.Setup(x => x.KeyDeleteAsync(redisKey, CommandFlags.None))
            .ReturnsAsync(true);

        // Act
        var result = await _provider.DeleteAsync(reference);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task StatAsync_ShouldReturnMetadata_WhenKeyExists()
    {
        // Arrange
        var key = "test-key";
        var reference = new StagingReference(StageName, key);
        var redisKey = (RedisKey)$"{StageName}:data:{key}";
        var metadata = new StagingMetadata(contentType: "text/plain");
        var metadataJson = JsonSerializer.Serialize(metadata);

        _mockDb.Setup(x => x.HashGetAsync(redisKey, (RedisValue)"metadata", CommandFlags.None))
            .ReturnsAsync((RedisValue)metadataJson);

        // Act
        var result = await _provider.StatAsync(reference);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("text/plain", result.ContentType);
    }
}
