using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Persistence;
using Aetheric.Provisioning.Worker;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class WebModuleConsumerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "web-consumer-" + Guid.NewGuid().ToString("N"));
    private static InstitutionDeploymentAttempt Deployment(string id, ImmutableDictionary<string, ProvisionedResource> resources,
        DeploymentParent? parent = null)
    {
        var source = new SourceProvenance(ModuleConsumerConfigurationStore.Repository(id), new string('a', 40), "institution.yaml", new string('b', 64));
        return new(InstitutionDeploymentAttempt.Identity(source.Repository, id, "production"), Guid.NewGuid(), id, id, "Description", "1.0.0", "production", parent,
            source, source, new string('c', 64), InstitutionDeploymentStatus.Succeeded, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, resources, resources.Keys.Select(id => new StepOutcome("owned:" + id, OutcomeStatus.Succeeded, "resource.created", [])).ToImmutableArray(), []);
    }

    [Theory]
    [InlineData("library")]
    [InlineData("archive")]
    public async Task Successful_deployments_export_complete_scoped_artifacts_and_reruns_preserve_credentials(string module)
    {
        var secrets = new EncryptedFileSecretStore(Path.Combine(_directory, "secrets"), RandomNumberGenerator.GetBytes(32));
        var mongoSecret = await secrets.GetOrCreateAsync("mongodb", "library-knowledge", default);
        var resource = module == "library"
            ? new ProvisionedResource("knowledge", "knowledge", "mongodb", ImmutableDictionary<string, string>.Empty.Add("database", "existing-library"), [mongoSecret])
                { Connection = new Dictionary<string, string> { ["Host"] = "mongo.example", ["Port"] = "27017", ["Username"] = "library-knowledge", ["AuthenticationDatabase"] = "admin" }.ToImmutableDictionary() }
            : new ProvisionedResource("archive", "archive", "s3", ImmutableDictionary<string, string>.Empty.Add("bucket", "existing-archive"), []);
        var record = Deployment(module, ImmutableDictionary<string, ProvisionedResource>.Empty.Add(resource.Id, resource));
        var output = new CaptureStore();
        using var routes = new ModuleConsumerConfigurationStore(new Dictionary<string, IConsumerConfigurationStore> { [module] = output }, "production");
        var archive = new CaptureArchive();
        var provisioner = new WebModuleConsumerProvisioner(secrets, routes, new FileInstitutionDeploymentStore(Path.Combine(_directory, "institutions")), archive);
        var roots = new Dictionary<string, RootCredentialPayload> { ["s3"] = new("seaweed.example", 8333, "root-admin", "root-secret") { Scheme = "http" } };
        var delivery = await provisioner.ProvisionAsync(record, roots, default);
        Assert.Equal(ConsumerDeliveryStatus.Ready, delivery.Status);
        var first = output.Configuration!;
        Assert.True(WebModuleConsumerProfiles.RequiredSettings(module).SetEquals(first.Settings.Keys));
        Assert.DoesNotContain("root-secret", JsonSerializer.Serialize(first));
        await provisioner.ProvisionAsync(record, roots, default);
        Assert.Equal(first.Settings.OrderBy(pair => pair.Key), output.Configuration!.Settings.OrderBy(pair => pair.Key));
        if (module == "library")
        {
            Assert.Equal("existing-library", first.Settings["Library:MongoDb:DatabaseName"]);
            Assert.Equal(await secrets.ReadAsync(mongoSecret, default), first.Settings["Library:MongoDb:Password"]);
        }
        else { Assert.Equal("existing-archive", archive.Bucket); Assert.Equal(2, archive.Calls); }
    }

    [Fact]
    public async Task Decisions_without_a_catalog_owner_cannot_export_or_mint_credentials()
    {
        var output = new CaptureStore(); var archive = new CaptureArchive();
        var provisioner = new WebModuleConsumerProvisioner(
            new EncryptedFileSecretStore(Path.Combine(_directory, "secrets"), RandomNumberGenerator.GetBytes(32)), output,
            new FileInstitutionDeploymentStore(Path.Combine(_directory, "institutions")), archive,
            new("existing-office", "Existing office", "members", "maintainers"));
        var record = Deployment("decisions", ImmutableDictionary<string, ProvisionedResource>.Empty);
        var exception = await Assert.ThrowsAsync<ConsumerProvisioningException>(() => provisioner.ProvisionAsync(record, new Dictionary<string, RootCredentialPayload>(), default));
        Assert.Equal("consumer.catalog_owner_required", exception.Code);
        Assert.Null(output.Configuration); Assert.Equal(0, archive.Calls);
    }

    [Fact]
    public async Task Decisions_uses_its_recorded_owner_and_exports_a_separate_read_only_directory_client()
    {
        var secrets = new EncryptedFileSecretStore(Path.Combine(_directory, "secrets"), RandomNumberGenerator.GetBytes(32));
        var store = new FileInstitutionDeploymentStore(Path.Combine(_directory, "institutions"));
        var mongo = await secrets.GetOrCreateAsync("mongodb", "campus-library", default);
        var post = await secrets.GetOrCreateAsync("rabbitmq", "campus-post", default);
        var resources = new Dictionary<string, ProvisionedResource>
        {
            ["library"] = new("library", "knowledge", "mongodb", ImmutableDictionary<string, string>.Empty.Add("database", "campus-library"), [mongo])
                { Connection = new Dictionary<string, string> { ["Host"] = "mongo.example", ["Port"] = "27017", ["Username"] = "campus-library", ["AuthenticationDatabase"] = "admin" }.ToImmutableDictionary() },
            ["archive"] = new("archive", "archive", "s3", ImmutableDictionary<string, string>.Empty.Add("bucket", "campus-archive"), []),
            ["post"] = new("post", "post", "rabbitmq", ImmutableDictionary<string, string>.Empty.Add("vhost", "campus-post"), [post])
                { Connection = ImmutableDictionary<string, string>.Empty.Add("Host", "broker.example").Add("Port", "15672") },
            ["workbench"] = new("workbench", "staging", "workbench", ImmutableDictionary<string, string>.Empty.Add("stage", "Drafts"), []),
            ["registry"] = new("registry", "identity", "keycloak", ImmutableDictionary<string, string>.Empty.Add("realm", "members"), [])
        }.ToImmutableDictionary();
        var owner = Deployment("library", resources) with { DeploymentId = InstitutionDeploymentAttempt.Identity(ModuleConsumerConfigurationStore.Repository("library"), "campus", "production"), InstitutionId = "campus" };
        await store.SaveAttemptAsync(owner, default);
        var record = Deployment("decisions", ImmutableDictionary<string, ProvisionedResource>.Empty,
            new(owner.DefinitionSource.Repository, owner.DefinitionSource.Commit, owner.DeploymentId, ImmutableDictionary<string, string>.Empty));
        var output = new CaptureStore(); var directory = new DirectoryClientHandler();
        var provisioner = new WebModuleConsumerProvisioner(secrets, output, store, new CaptureArchive(),
            publicOrigin: new Uri("https://forge.example/"), registryAuthority: new Uri("https://public-identity.example/"),
            registryFactory: root => new Aetheric.Provisioning.Keycloak.KeycloakResourceProvider(root, directory));
        var roots = new Dictionary<string, RootCredentialPayload>
        {
            ["s3"] = new("archive.example", 8333, "root", "archive-root-secret"),
            ["rabbitmq"] = new("broker.example", 15672, "root", "broker-root-secret"),
            ["keycloak"] = new("identity.example", 443, "root", "identity-root-secret")
        };
        var result = await provisioner.ProvisionAsync(record, roots, default, new("existing-office", "Existing office", "members", "maintainers"));
        Assert.Equal(ConsumerDeliveryStatus.Ready, result.Status);
        Assert.True(WebModuleConsumerProfiles.RequiredSettings("decisions").SetEquals(output.Configuration!.Settings.Keys));
        Assert.Equal(owner.DeploymentId, output.Configuration.Settings["Decisions:Owner:DeploymentId"]);
        Assert.Equal("campus-library", output.Configuration.Settings["Decisions:MongoDb:DatabaseName"]);
        Assert.Equal("https://public-identity.example", output.Configuration.Settings["Decisions:Directory:Keycloak:Authority"]);
        Assert.DoesNotContain(output.Configuration.Settings.Keys, key => key.StartsWith("Registry:"));
        Assert.DoesNotContain("manage-users", directory.Roles);
        var serialized = JsonSerializer.Serialize(output.Configuration);
        foreach (var root in roots.Values) Assert.DoesNotContain(root.Password, serialized);
    }

    [Fact]
    public async Task A_consumer_cannot_overwrite_another_deployment_or_environment_slot()
    {
        var output = new CaptureStore();
        using var routes = new ModuleConsumerConfigurationStore(new Dictionary<string, IConsumerConfigurationStore> { ["library"] = output }, "production");
        var config = new ConsumerConfiguration(1, "library", new string('a', 64), "library", "1.0.0", "production", new string('b', 64), ImmutableDictionary<string, string>.Empty);
        await Assert.ThrowsAsync<ConsumerProvisioningException>(() => routes.WriteAsync(config, default));
        Assert.Null(output.Configuration);
    }

    private sealed class CaptureStore : IConsumerConfigurationStore
    {
        public ConsumerConfiguration? Configuration;
        public Task WriteAsync(ConsumerConfiguration configuration, CancellationToken ct) { Configuration = configuration; return Task.CompletedTask; }
    }
    private sealed class CaptureArchive : IArchiveConsumerCredentials
    {
        public int Calls; public string? Bucket;
        public Task EnsureAsync(RootCredentialPayload root, string accessKey, string secretKey, string bucket, CancellationToken ct)
        { Calls++; Bucket = bucket; Assert.NotEqual(root.Password, secretKey); return Task.CompletedTask; }
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
