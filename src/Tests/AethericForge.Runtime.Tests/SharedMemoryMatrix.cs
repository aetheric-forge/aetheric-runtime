using AethericForge.Runtime.SharedMemory;
using AethericForge.Runtime.SharedMemory.Abstractions;

namespace AethericForge.Runtime.Tests;

public static class SharedMemoryMatrix
{
    public static IEnumerable<object[]> Cases()
    {
        // Always yield the in-memory shared memory
        yield return new object[] { new InMemorySharedMemory() };

        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
        if (!string.IsNullOrWhiteSpace(redisUrl))
        {
            yield return new object[] { new RedisSharedMemory(redisUrl) };
        }
    }
}
