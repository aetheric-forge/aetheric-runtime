using System.Collections.Immutable;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.S3;
using Amazon.S3;
using Amazon.S3.Model;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class S3ResourceProviderTests
{
    private static ResourceBinding Binding => new("s3",
        new Dictionary<string, string> { ["bucket"] = "test-bucket" }.ToImmutableDictionary(),
        ImmutableDictionary<string, SecretReference>.Empty);
    private static ResourceRequirement Resource => new("archive", "Archive", "archive", "owned");

    // Validate is pure - no connection is ever attempted - so a dummy credential is fine
    // regardless of whether a live server is available for the integration tests below.
    private static RootCredential DummyCredential => new("localhost", 9000, "unused", "unused");

    [Theory]
    [InlineData("bucket", "")]
    [InlineData("bucket", "ab")] // below the 3-character minimum
    [InlineData("bucket", "Has-Uppercase")]
    [InlineData("bucket", "-leading-hyphen")]
    [InlineData("bucket", "trailing-hyphen-")]
    [InlineData("bucket", "has a space")]
    [InlineData("extra", "value")]
    public void Invalid_bindings_are_rejected(string key, string value)
    {
        using var provider = new S3ResourceProvider(DummyCredential);
        Assert.NotEmpty(provider.Validate(Resource, Binding with { Settings = Binding.Settings.SetItem(key, value) }));
    }

    [Fact]
    public void Bindings_with_secrets_are_rejected()
    {
        using var provider = new S3ResourceProvider(DummyCredential);
        var binding = Binding with { Secrets = ImmutableDictionary<string, SecretReference>.Empty.Add("bucket", new("leaked")) };
        Assert.NotEmpty(provider.Validate(Resource, binding));
    }

    [Fact]
    public void Parent_owned_resources_are_rejected()
    {
        using var provider = new S3ResourceProvider(DummyCredential);
        Assert.NotEmpty(provider.Validate(Resource with { Ownership = "parent" }, Binding));
    }

    // Unlike Rabbit/Mongo/Keycloak, this provider mints no per-institution secret (v1 reuses the
    // root credential everywhere) - so the only identity to keep unique across runs is the
    // bucket name itself, not an institution-scoped derivative of it.
    [S3Fact]
    public async Task New_bucket_is_created_with_a_deny_insecure_transport_policy()
    {
        using var provider = new S3ResourceProvider(S3FactAttribute.RootCredential());
        var bucket = "test-" + Guid.NewGuid().ToString("N")[..16];
        var binding = Binding with { Settings = Binding.Settings.SetItem("bucket", bucket) };
        var context = new ProviderContext("plan", "development", "campus", Resource, binding, new InMemorySecrets());

        var result = await provider.EnsureAsync(context, default);

        Assert.False(result.AlreadyExists);
        Assert.Empty(result.Secrets);
        var policy = await PolicyAsync(bucket);
        Assert.Contains("DenyInsecureTransport", policy);
    }

    [S3Fact]
    public async Task Existing_bucket_is_recognized_and_the_policy_is_reasserted()
    {
        using var provider = new S3ResourceProvider(S3FactAttribute.RootCredential());
        var bucket = "test-" + Guid.NewGuid().ToString("N")[..16];
        var binding = Binding with { Settings = Binding.Settings.SetItem("bucket", bucket) };
        var context = new ProviderContext("plan", "development", "campus", Resource, binding, new InMemorySecrets());

        var first = await provider.EnsureAsync(context, default);
        var second = await provider.EnsureAsync(context, default);

        Assert.False(first.AlreadyExists);
        Assert.True(second.AlreadyExists);
        var policy = await PolicyAsync(bucket);
        Assert.Contains("DenyInsecureTransport", policy);
    }

    private static async Task<string> PolicyAsync(string bucket)
    {
        using var client = S3FactAttribute.Client();
        var response = await client.GetBucketPolicyAsync(bucket);
        return response.Policy;
    }

    private sealed class InMemorySecrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<SecretReference> GetOrCreateAsync(string scope, string name, CancellationToken ct)
        {
            var id = scope + ":" + name;
            if (!_values.ContainsKey(id)) _values[id] = Guid.NewGuid().ToString("N");
            return Task.FromResult(new SecretReference(id));
        }

        public Task<string> ReadAsync(SecretReference reference, CancellationToken ct) => Task.FromResult(_values[reference.Id]);
    }
}

public sealed class S3FactAttribute : FactAttribute
{
    // Format: http://accesskey:secretkey@127.0.0.1:9000 - userinfo carries the root credential.
    public static bool HasCredential => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PROVISIONING_TEST_S3"));

    public S3FactAttribute()
    {
        if (!HasCredential) Skip = "Set PROVISIONING_TEST_S3 to a http://accessKey:secretKey@host:port root connection for an isolated MinIO server.";
    }

    private static Uri ParsedUri() => new(Environment.GetEnvironmentVariable("PROVISIONING_TEST_S3")!);

    public static RootCredential RootCredential()
    {
        var uri = ParsedUri();
        var parts = uri.UserInfo.Split(':');
        return new RootCredential(uri.Host, uri.Port, Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]))
        { S3 = new S3RootOptions(uri.Scheme) };
    }

    public static IAmazonS3 Client()
    {
        var uri = ParsedUri();
        var parts = uri.UserInfo.Split(':');
        var config = new AmazonS3Config { ServiceURL = uri.GetLeftPart(UriPartial.Authority), ForcePathStyle = true };
        return new AmazonS3Client(Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]), config);
    }
}
