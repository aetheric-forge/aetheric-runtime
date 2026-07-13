using System.Text;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Providers;
using AethericForge.Runtime.Models.Staging;
using AethericForge.Runtime.Services.Staging;

namespace AethericForge.Runtime.Tests;

public class StagingTests
{
    [Fact]
    public async Task StagingService_Routes_Operations_To_Reference_Stage()
    {
        var hot = new RecordingStagingProvider("hot");
        var warm = new RecordingStagingProvider("warm");
        var service = new StagingService([hot, warm]);

        var reference = await service.PutAsync(
            "hot",
            "docs/active.json",
            CreateStream("{\"status\":\"active\"}"),
            new StagingMetadata("application/json"));

        Assert.Equal("hot", reference.Stage);
        Assert.Equal("docs/active.json", reference.Key);
        Assert.Contains("put:docs/active.json", hot.Calls);
        Assert.Empty(warm.Calls);

        await service.ExistsAsync(reference);
        await service.StatAsync(reference);
        await service.OpenReadAsync(reference);
        await service.GetAsync(reference);
        await service.PinAsync(reference);
        await service.UnpinAsync(reference);
        await service.AcquireLockAsync(reference);
        await service.DeleteAsync(reference);

        Assert.Equal(
            ["put:docs/active.json", "exists:docs/active.json", "stat:docs/active.json", "read:docs/active.json", "get:docs/active.json", "pin:docs/active.json", "unpin:docs/active.json", "lock:docs/active.json", "delete:docs/active.json"],
            hot.Calls);
    }

    [Fact]
    public void StagingMetadata_Normalizes_Values_And_Attributes()
    {
        var metadata = new StagingMetadata(
            " application/json ",
            1024,
            " v1 ",
            DateTimeOffset.UtcNow,
            TimeSpan.FromHours(1),
            new Dictionary<string, string> { [" Cache-Control "] = "public" });

        Assert.Equal("application/json", metadata.ContentType);
        Assert.Equal(1024, metadata.ContentLength);
        Assert.Equal("v1", metadata.ETag);
        Assert.Equal(TimeSpan.FromHours(1), metadata.Expiration);
        Assert.Equal("public", metadata.Attributes["Cache-Control"]);
    }

    private static MemoryStream CreateStream(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value));
    }

    private sealed class RecordingStagingProvider : IStagingProvider
    {
        public RecordingStagingProvider(string stage)
        {
            Stage = stage;
        }

        public string Stage { get; }
        public List<string> Calls { get; } = [];

        public Task<IStagingReference> PutAsync(string key, Stream content, IStagingMetadata? metadata = null, CancellationToken ct = default)
        {
            Calls.Add($"put:{key}");
            return Task.FromResult<IStagingReference>(new StagingReference(Stage, key));
        }

        public Task<Stream> OpenReadAsync(IStagingReference reference, CancellationToken ct = default)
        {
            Calls.Add($"read:{reference.Key}");
            return Task.FromResult<Stream>(CreateStream("data"));
        }

        public Task<IStagingMetadata?> StatAsync(IStagingReference reference, CancellationToken ct = default)
        {
            Calls.Add($"stat:{reference.Key}");
            return Task.FromResult<IStagingMetadata?>(new StagingMetadata());
        }

        public Task<bool> ExistsAsync(IStagingReference reference, CancellationToken ct = default)
        {
            Calls.Add($"exists:{reference.Key}");
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(IStagingReference reference, CancellationToken ct = default)
        {
            Calls.Add($"delete:{reference.Key}");
            return Task.FromResult(true);
        }

        public Task<IStagingObject?> GetAsync(IStagingReference reference, CancellationToken ct = default)
        {
            Calls.Add($"get:{reference.Key}");
            return Task.FromResult<IStagingObject?>(new StagingObject(reference));
        }

        public Task PinAsync(IStagingReference reference, CancellationToken ct = default)
        {
            Calls.Add($"pin:{reference.Key}");
            return Task.CompletedTask;
        }

        public Task UnpinAsync(IStagingReference reference, CancellationToken ct = default)
        {
            Calls.Add($"unpin:{reference.Key}");
            return Task.CompletedTask;
        }

        public Task<IStagingLock> AcquireLockAsync(IStagingReference reference, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            Calls.Add($"lock:{reference.Key}");
            return Task.FromResult<IStagingLock>(new MockLock(reference));
        }
    }

    private sealed class MockLock : IStagingLock
    {
        public MockLock(IStagingReference reference) => Reference = reference;
        public IStagingReference Reference { get; }
        public bool IsAcquired => true;
        public Task ReleaseAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
