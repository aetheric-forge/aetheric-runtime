using AethericForge.Runtime.SharedMemory;
using AethericForge.Runtime.SharedMemory.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace AethericForge.Runtime.Tests;

public class SharedMemoryTests
{
    public static IEnumerable<object[]> Cases => SharedMemoryMatrix.Cases();

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Set_Then_Get_Returns_Same_Value(ISharedMemory memory)
    {
        var key = Guid.NewGuid().ToString();
        var value = new Ping { Id = 42, Message = "hello" };
        await memory.SetAsync(key, value);
        var result = await memory.GetAsync<Ping>(key);
        Assert.NotNull(result);
        Assert.Equal(value.Id, result.Id);
        Assert.Equal(value.Message, result.Message);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Remove_Deletes_Value(ISharedMemory memory)
    {
        var key = Guid.NewGuid().ToString();
        await memory.SetAsync(key, 1234);
        var removed = await memory.RemoveAsync(key);
        Assert.True(removed);
        Assert.False(await memory.ExistsAsync(key));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Exists_Works_As_Expected(ISharedMemory memory)
    {
        var key = Guid.NewGuid().ToString();
        Assert.False(await memory.ExistsAsync(key));
        await memory.SetAsync(key, "data");
        Assert.True(await memory.ExistsAsync(key));
    }

    [Fact]
    public void CreateConfigurationOptions_Parses_Redis_Uri_And_Sets_Defaults()
    {
        var connectionString = "redis://default:secret@localhost:6380/0";
        var options = RedisSharedMemory.CreateConfigurationOptions(connectionString);

        Assert.Single(options.EndPoints);
        Assert.Contains(options.EndPoints, endpoint => endpoint.ToString().Contains("localhost", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("default", options.User);
        Assert.Equal("secret", options.Password);
        Assert.False(options.AbortOnConnectFail);
        Assert.Equal(5000, options.ConnectTimeout);
        Assert.Equal(5000, options.SyncTimeout);
    }

    public record Ping { public int Id { get; set; } public string Message { get; set; } = string.Empty; }
}

// To test against Redis, set REDIS_URL before running tests:
// export REDIS_URL="redis://:password@localhost:6379"
