using System.Text;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Providers;
using AethericForge.Runtime.Models.Staging;
using AethericForge.Runtime.Providers.Staging.InMemory;
using AethericForge.Runtime.Services.Staging;

namespace AethericForge.Runtime.Tests.Staging;

public class StagingTests
{
    [Fact]
    public async Task StagingService_Routes_Operations_To_Reference_Stage()
    {
        var hot = new InMemoryStagingProvider("hot");
        var warm = new InMemoryStagingProvider("warm");
        var service = new StagingService([hot, warm]);

        var reference = await service.PutAsync(
            "hot",
            "docs/active.json",
            CreateStream("{\"status\":\"active\"}"),
            new StagingMetadata("application/json"));

        Assert.Equal("hot", reference.Stage);
        Assert.Equal("docs/active.json", reference.Key);
        
        Assert.True(await hot.ExistsAsync(reference));
        Assert.False(await warm.ExistsAsync(reference));

        await service.PinAsync(reference);
        await service.UnpinAsync(reference);
        await service.AcquireLockAsync(reference);
        await service.DeleteAsync(reference);

        Assert.False(await hot.ExistsAsync(reference));
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

}
