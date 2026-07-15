using System.Text;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Models.Archive.Primitives;
using AethericForge.Runtime.Providers.Archive.MongoDb;
using AethericForge.Runtime.Providers.Archive.S3;
using Xunit;

namespace AethericForge.Runtime.IntegrationTests;

public sealed class ArchiveProviderTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task MongoDb_round_trip_preserves_content_and_metadata()
    {
        var provider = new MongoDbArchiveProvider(
            EnvironmentConfiguration.Require("AF_E2E_MONGODB_URI"),
            EnvironmentConfiguration.Get("AF_E2E_MONGODB_DATABASE", "aetheric_runtime_e2e"),
            EnvironmentConfiguration.Get("AF_E2E_MONGODB_COLLECTION", "archive_objects"),
            "e2e-mongodb",
            directConnection: bool.Parse(EnvironmentConfiguration.Get("AF_E2E_MONGODB_DIRECT_CONNECTION", "false")));

        await AssertRoundTripAsync(provider);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task S3_round_trip_preserves_content_and_metadata()
    {
        using var client = EnvironmentConfiguration.CreateS3Client();
        var provider = new S3ArchiveProvider(
            client,
            "e2e-s3",
            EnvironmentConfiguration.Require("AF_E2E_S3_BUCKET"),
            EnvironmentConfiguration.Get("AF_E2E_S3_KEY_PREFIX", "aetheric-runtime-e2e"));

        await AssertRoundTripAsync(provider);
    }

    private static async Task AssertRoundTripAsync(IArchiveProvider provider)
    {
        var runId = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var key = $"runs/{runId}/round-trip.txt";
        var content = Encoding.UTF8.GetBytes($"Aetheric Runtime integration test {runId}");
        var metadata = new ArchiveMetadata(
            contentType: "text/plain",
            contentLength: content.Length,
            attributes: new Dictionary<string, string> { ["e2e-run-id"] = runId });

        var reference = await provider.PutAsync(key, new MemoryStream(content), metadata);

        try
        {
            Assert.True(await provider.ExistsAsync(reference));
            var storedMetadata = await provider.StatAsync(reference);
            Assert.NotNull(storedMetadata);
            Assert.Equal(content.Length, storedMetadata.ContentLength);
            Assert.Equal("text/plain", storedMetadata.ContentType);
            Assert.Equal(runId, storedMetadata.Attributes["e2e-run-id"]);

            await using var stored = await provider.RetrieveAsync(reference);
            using var buffer = new MemoryStream();
            await stored.CopyToAsync(buffer);
            Assert.Equal(content, buffer.ToArray());
        }
        finally
        {
            await provider.DeleteAsync(reference);
        }

        Assert.False(await provider.ExistsAsync(reference));
    }
}
