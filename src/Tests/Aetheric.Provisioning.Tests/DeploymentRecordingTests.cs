using System.Collections.Immutable;
using System.Security.Cryptography;
using Aetheric.Provisioning.Application;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Persistence;
using Aetheric.Provisioning.Worker;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class DeploymentRecordingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "recorded-deployment-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task Worker_records_executed_resources_and_retry_reuses_the_scoped_secret()
    {
        var store = new FileInstitutionDeploymentStore(Path.Combine(_root, "catalog"));
        var provider = new ScopedMongoProvider();
        var requestId = Guid.NewGuid();
        var requestedAt = DateTimeOffset.UtcNow;
        var roots = new Dictionary<string, RootCredentialPayload> { ["mongo"] = new("mongo.example", 27017, "root", "root-secret") };
        var first = Review(provider);
        Assert.True((await InstitutionDeploymentRequestConsumer.DeployOneAsync(first, null, default,
            store, requestId, requestedAt, roots)).Succeeded);
        var deploymentId = InstitutionDeploymentAttempt.Identity(first.Loaded!.Source.Definition.Provenance.Repository, "parallel-you", "test");
        var record = (await store.ReadAsync(deploymentId, default))!.Provisioned!;
        var library = record.Resources["library"];
        Assert.Equal("personal-library", library.Settings["database"]);
        Assert.Equal("parallel-you-library", library.Connection["Username"]);
        Assert.Equal("admin", library.Connection["AuthenticationDatabase"]);
        Assert.Single(library.Secrets);
        var second = Review(provider);
        Assert.True((await InstitutionDeploymentRequestConsumer.DeployOneAsync(second, null, default,
            store, requestId, requestedAt, roots)).Succeeded);
        Assert.Equal(1, provider.Executions);
        var text = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(Path.Combine(_root, "catalog"), "*.institution.json")));
        Assert.DoesNotContain("root-secret", text);
        Assert.DoesNotContain(await Secrets().ReadAsync(library.Secrets.Single(), default), text);
    }

    [Fact]
    public async Task Retry_does_not_export_a_checkpoint_to_a_different_backing_endpoint()
    {
        var store = new FileInstitutionDeploymentStore(Path.Combine(_root, "catalog"));
        var provider = new ScopedMongoProvider();
        var requestedAt = DateTimeOffset.UtcNow;
        var roots = new Dictionary<string, RootCredentialPayload> { ["mongo"] = new("old-mongo.example", 27017, "root", "secret") };
        Assert.True((await InstitutionDeploymentRequestConsumer.DeployOneAsync(Review(provider), null, default,
            store, Guid.NewGuid(), requestedAt, roots)).Succeeded);
        roots["mongo"] = roots["mongo"] with { Host = "new-mongo.example" };
        var retry = await InstitutionDeploymentRequestConsumer.DeployOneAsync(Review(provider), null, default,
            store, Guid.NewGuid(), requestedAt.AddSeconds(1), roots);
        Assert.False(retry.Succeeded);
        Assert.Equal(new[] { "deployment.connection_changed" }, retry.Issues);
        Assert.Equal(1, provider.Executions);
    }

    private EncryptedFileSecretStore Secrets() => new(Path.Combine(_root, "secrets"), _key);

    private ProvisioningReview Review(ScopedMongoProvider provider)
    {
        var review = new ProvisioningReview(new UnusedSource(), new InstitutionYamlReader(), new ProvisioningPlanner([provider]),
            new ProvisioningEngine([provider], new NoParentCapabilityResolver(), new FileRunStateStore(Path.Combine(_root, "runs")), Secrets()));
        review.LoadBundle(InlineSourceDocuments.Build("parallel-you", new InstitutionConfig("""
            {"descriptor":{"id":"parallel-you","name":"Parallel You","version":"1.0.0","description":"Personal practice"},
             "dependencies":[],"domains":[],"organizations":[],"roles":[],"capabilities":[],
             "resources":[{"id":"library","name":"Library","description":"Personal artifacts","type":"knowledge","ownership":"owned"}],
             "workflows":[],"policies":[],"initialState":{"configuration":{}}}
            """, """
            {"institution":"parallel-you","version":"1.0.0","deployment":{"name":"test"},
             "bindings":{"library":{"provider":"mongodb","database":"personal-library"}}}
            """)));
        return review;
    }

    private sealed class ScopedMongoProvider : IResourceProvider
    {
        public string Key => "mongodb";
        public int Executions { get; private set; }
        public ImmutableArray<ValidationIssue> Validate(ResourceRequirement resource, ResourceBinding binding) => [];
        public async Task<ProviderResult> EnsureAsync(ProviderContext context, CancellationToken ct)
        {
            Executions++;
            return new(false, [await context.Secrets.GetOrCreateAsync("mongodb", context.InstitutionId + "-" + context.Resource.Id, ct)]);
        }
    }

    private sealed class UnusedSource : IDefinitionSource
    {
        public Task<SourceLoadResult> LoadAsync(DefinitionSourceRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
