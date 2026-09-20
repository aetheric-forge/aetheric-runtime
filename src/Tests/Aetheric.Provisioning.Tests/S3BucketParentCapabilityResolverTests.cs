using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.S3;
using Amazon.S3.Model;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class S3BucketParentCapabilityResolverTests
{
    private static ParentContext Parent => new("https://example.test/fixture", "0000000000000000000000000000000000000000");

    [S3Fact]
    public async Task Unrecognized_contracts_are_always_unavailable()
    {
        var bucket = await CreateBucketAsync();
        using var resolver = new S3BucketParentCapabilityResolver(S3FactAttribute.RootCredential(), bucket);
        Assert.False(await resolver.IsAvailableAsync(Parent, "ISomethingElse", "campus.archive", default));
    }

    [S3Fact]
    public async Task An_existing_bucket_is_available()
    {
        var bucket = await CreateBucketAsync();
        using var resolver = new S3BucketParentCapabilityResolver(S3FactAttribute.RootCredential(), bucket);
        Assert.True(await resolver.IsAvailableAsync(Parent, "IArchive", "campus.archive", default));
    }

    [S3Fact]
    public async Task A_bucket_that_was_never_created_is_unavailable()
    {
        using var resolver = new S3BucketParentCapabilityResolver(S3FactAttribute.RootCredential(), "nonexistent-" + Guid.NewGuid().ToString("N"));
        Assert.False(await resolver.IsAvailableAsync(Parent, "IArchive", "campus.archive", default));
    }

    private static async Task<string> CreateBucketAsync()
    {
        var bucket = "test-" + Guid.NewGuid().ToString("N")[..16];
        using var client = S3FactAttribute.Client();
        await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        return bucket;
    }
}
