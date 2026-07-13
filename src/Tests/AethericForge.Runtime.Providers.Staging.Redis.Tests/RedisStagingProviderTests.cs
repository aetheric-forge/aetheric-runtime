using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;
using AethericForge.Runtime.Models.Staging;
using AethericForge.Runtime.Providers.Staging.Redis;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;
using System.Text;

namespace AethericForge.Runtime.Providers.Staging.Redis.Tests;

public class RedisStagingProviderTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer = new RedisBuilder().Build();

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task RedisStagingProvider_Stages_And_Retrieves()
    {
        var connectionString = _redisContainer.GetConnectionString();
        using var redis = ConnectionMultiplexer.Connect(connectionString);
        var provider = new RedisStagingProvider(redis, "cache");

        var content = "hello world";
        var metadata = new StagingMetadata("text/plain");
        
        var reference = await provider.PutAsync("test.txt", CreateStream(content), metadata);
        
        Assert.Equal("cache", reference.Stage);
        Assert.Equal("test.txt", reference.Key);

        using var retrievedStream = await provider.OpenReadAsync(reference);
        Assert.NotNull(retrievedStream);
        
        using var reader = new StreamReader(retrievedStream);
        var retrievedContent = await reader.ReadToEndAsync();
        
        Assert.Equal(content, retrievedContent);
    }

    [Fact]
    public async Task RedisStagingProvider_Retrieves_Object()
    {
        var connectionString = _redisContainer.GetConnectionString();
        using var redis = ConnectionMultiplexer.Connect(connectionString);
        var provider = new RedisStagingProvider(redis, "cache");

        var key = "obj-test";
        var metadata = new StagingMetadata("application/json");
        await provider.PutAsync(key, CreateStream("{}"), metadata);

        var reference = new StagingReference("cache", key);
        var obj = await provider.GetAsync(reference);

        Assert.NotNull(obj);
        Assert.Equal(reference, obj.Reference);
        Assert.Equal("application/json", obj.Metadata.ContentType);
    }

    [Fact]
    public async Task RedisStagingProvider_Pins_And_Unpins()
    {
        var connectionString = _redisContainer.GetConnectionString();
        using var redis = ConnectionMultiplexer.Connect(connectionString);
        var provider = new RedisStagingProvider(redis, "cache");

        var expiration = TimeSpan.FromMinutes(5);
        var metadata = new StagingMetadata(expiration: expiration);
        
        var reference = await provider.PutAsync("pin-test", CreateStream("data"), metadata);
        
        var db = redis.GetDatabase();
        var initialTtl = await db.KeyTimeToLiveAsync($"cache:data:pin-test");
        Assert.NotNull(initialTtl);

        await provider.PinAsync(reference);
        
        var pinnedTtl = await db.KeyTimeToLiveAsync($"cache:data:pin-test");
        Assert.Null(pinnedTtl); // Null TTL in StackExchange.Redis means no expiry (persistent)

        await provider.UnpinAsync(reference);
        
        var unpinnedTtl = await db.KeyTimeToLiveAsync($"cache:data:pin-test");
        Assert.NotNull(unpinnedTtl);
    }

    [Fact]
    public async Task RedisStagingProvider_Locks_And_Unlocks()
    {
        var connectionString = _redisContainer.GetConnectionString();
        using var redis = ConnectionMultiplexer.Connect(connectionString);
        var provider = new RedisStagingProvider(redis, "cache");

        var reference = new StagingReference("cache", "lock-test");

        await using (var stagingLock = await provider.AcquireLockAsync(reference, TimeSpan.FromSeconds(10)))
        {
            Assert.True(stagingLock.IsAcquired);

            await using (var secondLock = await provider.AcquireLockAsync(reference, TimeSpan.FromSeconds(10)))
            {
                Assert.False(secondLock.IsAcquired);
            }

            await stagingLock.ReleaseAsync();
            Assert.False(stagingLock.IsAcquired);

            await using (var thirdLock = await provider.AcquireLockAsync(reference, TimeSpan.FromSeconds(10)))
            {
                Assert.True(thirdLock.IsAcquired);
            }
        }
    }

    [Fact]
    public async Task RedisStagingProvider_Throws_On_Wrong_Stage()
    {
        var connectionString = _redisContainer.GetConnectionString();
        using var redis = ConnectionMultiplexer.Connect(connectionString);
        var provider = new RedisStagingProvider(redis, "cache");

        var wrongReference = new StagingReference("wrong-stage", "test");

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.OpenReadAsync(wrongReference));
    }

    [Fact]
    public async Task RedisStagingProvider_Populates_Metadata()
    {
        var connectionString = _redisContainer.GetConnectionString();
        using var redis = ConnectionMultiplexer.Connect(connectionString);
        var provider = new RedisStagingProvider(redis, "cache");

        var content = "metadata test";
        var key = "metadata-test.txt";
        
        var reference = await provider.PutAsync(key, CreateStream(content));
        
        var metadata = await provider.StatAsync(reference);
        
        Assert.NotNull(metadata);
        Assert.Equal(content.Length, metadata.ContentLength);
        Assert.NotNull(metadata.LastModifiedUtc);
        Assert.True((DateTimeOffset.UtcNow - metadata.LastModifiedUtc.Value).TotalSeconds < 5);
    }

    [Fact]
    public async Task RedisStagingProvider_Unpin_Restores_Original_Expiration()
    {
        var connectionString = _redisContainer.GetConnectionString();
        using var redis = ConnectionMultiplexer.Connect(connectionString);
        var provider = new RedisStagingProvider(redis, "cache");

        var expiration = TimeSpan.FromMinutes(10);
        var metadata = new StagingMetadata(expiration: expiration);
        var key = "unpin-restore-test";
        
        var reference = await provider.PutAsync(key, CreateStream("data"), metadata);
        
        await provider.PinAsync(reference);
        var db = redis.GetDatabase();
        var pinnedTtl = await db.KeyTimeToLiveAsync($"cache:data:{key}");
        Assert.Null(pinnedTtl);

        await provider.UnpinAsync(reference);
        var unpinnedTtl = await db.KeyTimeToLiveAsync($"cache:data:{key}");
        Assert.NotNull(unpinnedTtl);
        // Should be close to 10 minutes, not 24 hours
        Assert.True(unpinnedTtl.Value.TotalMinutes <= 10);
        Assert.True(unpinnedTtl.Value.TotalMinutes > 9);
    }

    private static MemoryStream CreateStream(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value));
    }
}
