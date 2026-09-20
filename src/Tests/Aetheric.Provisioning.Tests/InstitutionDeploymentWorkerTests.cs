using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Persistence;
using Aetheric.Provisioning.Simulation;
using Aetheric.Provisioning.Worker;
using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Models.Post;
using AethericForge.Runtime.Providers.Post.RabbitMq;
using Xunit;

namespace Aetheric.Provisioning.Tests;

/// <summary>
/// Proves the message pipe end-to-end against a real RabbitMQ broker: publishing a
/// InstitutionDeploymentRequested drives the worker's consumer through the actual plan pipeline
/// (ProvisioningReview/Planner/Engine, not a mock), and the correct InstitutionDeploymentCompleted
/// comes back. One test proves the still-correct provider.unsupported failure for a request that
/// supplies no root credentials; the other proves a real success - the post-office resource
/// actually gets created (vhost/user/permissions), verified independently via the management API.
/// </summary>
public sealed class InstitutionDeploymentWorkerTests
{
    private static InstitutionDeploymentRequestConsumer BuildConsumer(IPostProvider postProvider, IDefinitionSource source, string tempDirectory) =>
        new(postProvider, source, new InstitutionYamlReader(),
            new FileRunStateStore(Path.Combine(tempDirectory, "run-state")),
            new EncryptedFileSecretStore(Path.Combine(tempDirectory, "secrets"), RandomNumberGenerator.GetBytes(32)));

    [RabbitMqFact]
    public async Task Worker_consumer_reports_provider_unsupported_when_no_credentials_are_supplied()
    {
        var connection = Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!;
        await using var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, connection);

        var tempDirectory = Path.Combine(Path.GetTempPath(), "aetheric-provisioning-worker-test-" + Guid.NewGuid().ToString("N"));
        var consumer = BuildConsumer(postProvider, new FixtureSource(), tempDirectory);

        // RabbitMQ fan-out delivers every published message to every subscriber of a reference,
        // and this test's own PostProvider instance is not the only one that may be concurrently
        // subscribed to the shared ProvisioningPost.RequestReference()/ResultReference() -
        // MultiResultConsumer demultiplexes by RequestId so a concurrently-running test's
        // messages can never be mistaken for this test's own.
        var multiResult = new MultiResultConsumer();
        await postProvider.SubscribeAsync(ProvisioningPost.ResultReference(), multiResult);
        await postProvider.SubscribeAsync(ProvisioningPost.RequestReference(), consumer);

        try
        {
            var request = new InstitutionDeploymentRequested(
                Guid.NewGuid(),
                "https://github.com/aetheric-forge/aetheric-runtime",
                "main",
                "institution/campus.yaml",
                "institution/campus.bindings.yaml",
                new Dictionary<string, RootCredentialPayload>(),
                null,
                DateTimeOffset.UtcNow);
            var resultReceived = multiResult.Expect(request.RequestId);
            var envelope = new PostEnvelope<InstitutionDeploymentRequested>(
                ProvisioningPost.RequestReference(), request, new PostMetadata());
            await postProvider.PublishAsync(envelope);

            var completed = await resultReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(request.RequestId, completed.RequestId);
            Assert.False(completed.Succeeded);
            Assert.NotEmpty(completed.Issues);
            Assert.Contains(completed.Issues, issue => issue.StartsWith("provider.unsupported", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [RabbitMqFact]
    public async Task Worker_consumer_actually_provisions_post_office_when_rabbitmq_credentials_are_supplied()
    {
        var connection = Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!;
        await using var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, connection);

        var vhost = "test-" + Guid.NewGuid().ToString("N");
        var tempDirectory = Path.Combine(Path.GetTempPath(), "aetheric-provisioning-worker-test-" + Guid.NewGuid().ToString("N"));
        var consumer = BuildConsumer(postProvider, new PostOfficeOnlySource(vhost), tempDirectory);

        var multiResult = new MultiResultConsumer();
        await postProvider.SubscribeAsync(ProvisioningPost.ResultReference(), multiResult);
        await postProvider.SubscribeAsync(ProvisioningPost.RequestReference(), consumer);

        var management = ManagementCredential();
        using var managementClient = new HttpClient { BaseAddress = new Uri($"http://{management.Host}:{management.Port}/") };
        managementClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{management.Username}:{management.Password}")));

        try
        {
            var request = new InstitutionDeploymentRequested(
                Guid.NewGuid(),
                "https://example.test/fixture",
                "0000000000000000000000000000000000000000",
                "institution/campus.yaml",
                "institution/campus.bindings.yaml",
                new Dictionary<string, RootCredentialPayload> { ["rabbitmq"] = management },
                null,
                DateTimeOffset.UtcNow);
            var resultReceived = multiResult.Expect(request.RequestId);
            var envelope = new PostEnvelope<InstitutionDeploymentRequested>(
                ProvisioningPost.RequestReference(), request, new PostMetadata());
            await postProvider.PublishAsync(envelope);

            var completed = await resultReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(request.RequestId, completed.RequestId);
            Assert.True(completed.Succeeded, string.Join("; ", completed.Issues));
            Assert.Empty(completed.Issues);

            using var vhostResponse = await managementClient.GetAsync($"api/vhosts/{Uri.EscapeDataString(vhost)}");
            Assert.Equal(HttpStatusCode.OK, vhostResponse.StatusCode);
            using var permissionsResponse = await managementClient.GetAsync($"api/permissions/{Uri.EscapeDataString(vhost)}/campus-post-office");
            Assert.Equal(HttpStatusCode.OK, permissionsResponse.StatusCode);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
            await managementClient.DeleteAsync($"api/vhosts/{Uri.EscapeDataString(vhost)}");
            await managementClient.DeleteAsync("api/users/campus-post-office");
        }
    }

    /// <summary>
    /// Proves the four message-driven Stage 6 providers work together through one real
    /// plan/execute run, not just individually - archive/library/post-office/registry, the whole
    /// campus minus Workbench (which this Worker deliberately never builds a provider for; see
    /// InstitutionDeploymentRequestConsumer's doc comment).
    /// </summary>
    [AllStage6ProvidersFact]
    public async Task Worker_consumer_provisions_all_four_message_driven_resources_together()
    {
        var connection = Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!;
        await using var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, connection);

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var vhost = "test-" + suffix;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "aetheric-provisioning-worker-test-" + Guid.NewGuid().ToString("N"));
        var consumer = BuildConsumer(postProvider, new FourResourceSource(suffix), tempDirectory);

        var multiResult = new MultiResultConsumer();
        await postProvider.SubscribeAsync(ProvisioningPost.ResultReference(), multiResult);
        await postProvider.SubscribeAsync(ProvisioningPost.RequestReference(), consumer);

        var management = ManagementCredential();
        using var managementClient = new HttpClient { BaseAddress = new Uri($"http://{management.Host}:{management.Port}/") };
        managementClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{management.Username}:{management.Password}")));

        try
        {
            var request = new InstitutionDeploymentRequested(
                Guid.NewGuid(),
                "https://example.test/fixture",
                "0000000000000000000000000000000000000000",
                "institution/campus.yaml",
                "institution/campus.bindings.yaml",
                new Dictionary<string, RootCredentialPayload>
                {
                    ["rabbitmq"] = management,
                    ["mongo"] = MongoCredential(),
                    ["keycloak"] = KeycloakCredential(),
                    ["s3"] = S3Credential(),
                },
                null,
                DateTimeOffset.UtcNow);
            var resultReceived = multiResult.Expect(request.RequestId);
            var envelope = new PostEnvelope<InstitutionDeploymentRequested>(
                ProvisioningPost.RequestReference(), request, new PostMetadata());
            await postProvider.PublishAsync(envelope);

            var completed = await resultReceived.Task.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(request.RequestId, completed.RequestId);
            Assert.True(completed.Succeeded, string.Join("; ", completed.Issues));
            Assert.Empty(completed.Issues);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
            await managementClient.DeleteAsync($"api/vhosts/{Uri.EscapeDataString(vhost)}");
            await managementClient.DeleteAsync("api/users/campus-post-office");
        }
    }

    /// <summary>
    /// Proves the University->Campus parent-contract mechanism end to end against live
    /// infrastructure: deploying University first (its Keycloak realm gets created), then
    /// deploying Campus with a *real* University parent identity pointing at that realm, and
    /// confirming CheckParent actually passes - not a stub, not ParentContext.Capabilities
    /// trusted blindly, an actual live Keycloak lookup via KeycloakRealmParentCapabilityResolver.
    /// </summary>
    [AllStage6ProvidersFact]
    public async Task Worker_consumer_provisions_campus_under_a_real_university_parent()
    {
        var connection = Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!;
        await using var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, connection);

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var realm = "test-university-" + suffix;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "aetheric-provisioning-worker-test-" + Guid.NewGuid().ToString("N"));

        var multiResult = new MultiResultConsumer();
        await postProvider.SubscribeAsync(ProvisioningPost.ResultReference(), multiResult);

        // A single consumer, path-dispatched by its IDefinitionSource - matching how the real
        // Worker actually runs (one consumer, one PublicGitHubSource resolving whatever
        // DefinitionPath a message names). Two consumers separately subscribed to the same
        // RequestReference() would both receive every published message (fan-out, not
        // competing-consumers) and race to answer it with the wrong fixture.
        var consumer = BuildConsumer(postProvider, new UniversityThenCampusSource(realm, suffix), tempDirectory);
        await postProvider.SubscribeAsync(ProvisioningPost.RequestReference(), consumer);

        try
        {
            var universityRequestId = Guid.NewGuid();
            var universityWait = multiResult.Expect(universityRequestId);
            await postProvider.PublishAsync(new PostEnvelope<InstitutionDeploymentRequested>(
                ProvisioningPost.RequestReference(),
                new InstitutionDeploymentRequested(universityRequestId, "https://example.test/fixture",
                    "0000000000000000000000000000000000000000", "institution/university.yaml", "institution/university.bindings.yaml",
                    new Dictionary<string, RootCredentialPayload> { ["keycloak"] = KeycloakCredential() }, null, DateTimeOffset.UtcNow),
                new PostMetadata()));
            var universityCompleted = await universityWait.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(universityCompleted.Succeeded, string.Join("; ", universityCompleted.Issues));

            var campusRequestId = Guid.NewGuid();
            var campusWait = multiResult.Expect(campusRequestId);
            var parent = new ParentIdentity("https://example.test/fixture", "0000000000000000000000000000000000000000", realm);
            await postProvider.PublishAsync(new PostEnvelope<InstitutionDeploymentRequested>(
                ProvisioningPost.RequestReference(),
                new InstitutionDeploymentRequested(campusRequestId, "https://example.test/fixture",
                    "0000000000000000000000000000000000000000", "institution/campus.yaml", "institution/campus.bindings.yaml",
                    new Dictionary<string, RootCredentialPayload>
                    {
                        ["rabbitmq"] = ManagementCredential(), ["mongo"] = MongoCredential(),
                        ["keycloak"] = KeycloakCredential(), ["s3"] = S3Credential(),
                    }, parent, DateTimeOffset.UtcNow),
                new PostMetadata()));
            var campusCompleted = await campusWait.Task.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(campusCompleted.Succeeded, string.Join("; ", campusCompleted.Issues));
            Assert.Empty(campusCompleted.Issues);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    /// <summary>
    /// The companion negative case: Campus is given a parent identity pointing at a realm that
    /// was never created. CheckParent must fail specifically with parent.unavailable on the
    /// IRegistrar step - proving the resolver does real live verification, not a rubber stamp -
    /// and Campus's other owned resources must come back Blocked rather than silently succeeding,
    /// per ProvisioningPlanner's existing all-or-nothing DependsOn behavior.
    /// </summary>
    [AllStage6ProvidersFact]
    public async Task Worker_consumer_blocks_campus_when_the_university_registry_is_unavailable()
    {
        var connection = Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!;
        await using var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, connection);

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var neverCreatedRealm = "test-university-never-created-" + suffix;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "aetheric-provisioning-worker-test-" + Guid.NewGuid().ToString("N"));

        var multiResult = new MultiResultConsumer();
        await postProvider.SubscribeAsync(ProvisioningPost.ResultReference(), multiResult);

        try
        {
            var campusConsumer = BuildConsumer(postProvider, new CampusUnderUniversitySource(suffix), tempDirectory);
            await postProvider.SubscribeAsync(ProvisioningPost.RequestReference(), campusConsumer);

            var campusRequestId = Guid.NewGuid();
            var campusWait = multiResult.Expect(campusRequestId);
            var parent = new ParentIdentity("https://example.test/fixture", "0000000000000000000000000000000000000000", neverCreatedRealm);
            await postProvider.PublishAsync(new PostEnvelope<InstitutionDeploymentRequested>(
                ProvisioningPost.RequestReference(),
                new InstitutionDeploymentRequested(campusRequestId, "https://example.test/fixture",
                    "0000000000000000000000000000000000000000", "institution/campus.yaml", "institution/campus.bindings.yaml",
                    new Dictionary<string, RootCredentialPayload>
                    {
                        ["rabbitmq"] = ManagementCredential(), ["mongo"] = MongoCredential(),
                        ["keycloak"] = KeycloakCredential(), ["s3"] = S3Credential(),
                    }, parent, DateTimeOffset.UtcNow),
                new PostMetadata()));
            var campusCompleted = await campusWait.Task.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.False(campusCompleted.Succeeded);
            Assert.Contains(campusCompleted.Issues, issue => issue.StartsWith("parent:IRegistrar: Failed (parent.unavailable)", StringComparison.Ordinal));
            Assert.Contains(campusCompleted.Issues, issue => issue.StartsWith("owned:archive: Blocked", StringComparison.Ordinal));
            Assert.Contains(campusCompleted.Issues, issue => issue.StartsWith("owned:library: Blocked", StringComparison.Ordinal));
            Assert.Contains(campusCompleted.Issues, issue => issue.StartsWith("owned:post-office: Blocked", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static RootCredentialPayload ManagementCredential()
    {
        var amqp = new Uri(Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!);
        var userInfo = amqp.UserInfo.Split(':', 2);
        // RabbitMQ's management plugin listens on a fixed port independent of the AMQP port.
        return new RootCredentialPayload(amqp.Host, 15672, Uri.UnescapeDataString(userInfo[0]), Uri.UnescapeDataString(userInfo[1]));
    }

    private static RootCredentialPayload MongoCredential()
    {
        var uri = new MongoDB.Driver.MongoUrl(Environment.GetEnvironmentVariable("PROVISIONING_TEST_MONGO")!);
        return new RootCredentialPayload(uri.Server!.Host, uri.Server.Port, uri.Username, uri.Password, AuthDatabase: uri.AuthenticationSource ?? "admin");
    }

    private static RootCredentialPayload KeycloakCredential()
    {
        var uri = new Uri(Environment.GetEnvironmentVariable("PROVISIONING_TEST_KEYCLOAK")!);
        var userInfo = uri.UserInfo.Split(':', 2);
        return new RootCredentialPayload(uri.Host, uri.Port, Uri.UnescapeDataString(userInfo[0]), Uri.UnescapeDataString(userInfo[1]), Scheme: uri.Scheme);
    }

    private static RootCredentialPayload S3Credential()
    {
        var uri = new Uri(Environment.GetEnvironmentVariable("PROVISIONING_TEST_S3")!);
        var userInfo = uri.UserInfo.Split(':', 2);
        return new RootCredentialPayload(uri.Host, uri.Port, Uri.UnescapeDataString(userInfo[0]), Uri.UnescapeDataString(userInfo[1]), Scheme: uri.Scheme);
    }

    private static SourceDocument Document(string text, string path)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return new SourceDocument(text, new SourceProvenance("https://example.test/fixture", "0000000000000000000000000000000000000000", path, hash));
    }

    private sealed class FixtureSource : IDefinitionSource
    {
        public Task<SourceLoadResult> LoadAsync(DefinitionSourceRequest request, CancellationToken ct = default)
            => Task.FromResult(new SourceLoadResult(BundledDecisionsSource.Load(), []));
    }

    /// <summary>A minimal, hand-built institution with only the one resource this stage has a real provider for.</summary>
    private sealed class PostOfficeOnlySource(string vhost) : IDefinitionSource
    {
        public Task<SourceLoadResult> LoadAsync(DefinitionSourceRequest request, CancellationToken ct = default)
        {
            var definition = Document(JsonSerializer.Serialize(new
            {
                descriptor = new { id = "campus", name = "Campus", version = "1.0.0", description = "Test fixture campus with only Post Office." },
                // domains/capabilities/organizations/roles/workflows/policies are all required
                // sections (InstitutionYamlReader.Read reads each via Entries(), which throws
                // yaml.required if the key is missing at all) - empty arrays, not omission, is
                // how "none" is spelled.
                domains = Array.Empty<object>(),
                capabilities = Array.Empty<object>(),
                organizations = Array.Empty<object>(),
                roles = Array.Empty<object>(),
                resources = new[] { new { id = "post-office", name = "Post Office", description = "Test post office resource.", type = "post", ownership = "owned" } },
                workflows = Array.Empty<object>(),
                policies = Array.Empty<object>(),
                initialState = new { configuration = new { } }
            }), "institution/campus.yaml");
            var bindings = Document(JsonSerializer.Serialize(new
            {
                institution = "campus",
                version = "1.0.0",
                deployment = new { name = "test" },
                bindings = new Dictionary<string, object>
                {
                    ["post-office"] = new { provider = "rabbitmq", vhost }
                }
            }), "institution/campus.bindings.yaml");
            return Task.FromResult(new SourceLoadResult(new SourceBundle(definition, bindings), []));
        }
    }

    /// <summary>A hand-built institution with the four resources this Worker builds real providers for (no Workbench).</summary>
    private sealed class FourResourceSource(string suffix) : IDefinitionSource
    {
        public Task<SourceLoadResult> LoadAsync(DefinitionSourceRequest request, CancellationToken ct = default)
        {
            var definition = Document(JsonSerializer.Serialize(new
            {
                descriptor = new { id = "campus", name = "Campus", version = "1.0.0", description = "Test fixture campus with the four message-driven resources." },
                domains = Array.Empty<object>(),
                capabilities = Array.Empty<object>(),
                organizations = Array.Empty<object>(),
                roles = Array.Empty<object>(),
                resources = new[]
                {
                    new { id = "archive", name = "Archive", description = "Test archive resource.", type = "archive", ownership = "owned" },
                    new { id = "library", name = "Library", description = "Test library resource.", type = "knowledge", ownership = "owned" },
                    new { id = "post-office", name = "Post Office", description = "Test post office resource.", type = "post", ownership = "owned" },
                    new { id = "registry", name = "Registry", description = "Test registry resource.", type = "identity", ownership = "owned" },
                },
                workflows = Array.Empty<object>(),
                policies = Array.Empty<object>(),
                initialState = new { configuration = new { } }
            }), "institution/campus.yaml");
            var bindings = Document(JsonSerializer.Serialize(new
            {
                institution = "campus",
                version = "1.0.0",
                deployment = new { name = "test" },
                bindings = new Dictionary<string, object>
                {
                    ["archive"] = new { provider = "s3", bucket = "test-" + suffix },
                    ["library"] = new { provider = "mongodb", database = "test-" + suffix },
                    ["post-office"] = new { provider = "rabbitmq", vhost = "test-" + suffix },
                    ["registry"] = new { provider = "keycloak", realm = "test-" + suffix },
                }
            }), "institution/campus.bindings.yaml");
            return Task.FromResult(new SourceLoadResult(new SourceBundle(definition, bindings), []));
        }
    }

    /// <summary>
    /// Dispatches by request.DefinitionPath - a single University fixture and a single Campus
    /// fixture behind one IDefinitionSource, matching how the real Worker's one consumer (backed
    /// by PublicGitHubSource) resolves whatever path a message names, rather than two consumers
    /// each bound to a different hardcoded fixture (which would both receive every published
    /// message - fan-out, not competing-consumers - and race to answer with the wrong one).
    /// </summary>
    private sealed class UniversityThenCampusSource(string realm, string suffix) : IDefinitionSource
    {
        public Task<SourceLoadResult> LoadAsync(DefinitionSourceRequest request, CancellationToken ct = default) =>
            request.DefinitionPath == "institution/university.yaml"
                ? University()
                : new CampusUnderUniversitySource(suffix).LoadAsync(request, ct);

        private Task<SourceLoadResult> University()
        {
            var definition = Document(JsonSerializer.Serialize(new
            {
                descriptor = new { id = "university", name = "University", version = "1.0.0", description = "Test fixture university." },
                domains = Array.Empty<object>(),
                capabilities = Array.Empty<object>(),
                organizations = Array.Empty<object>(),
                roles = Array.Empty<object>(),
                resources = new[] { new { id = "registry", name = "Registry", description = "Test university registry.", type = "identity", ownership = "owned" } },
                workflows = Array.Empty<object>(),
                policies = Array.Empty<object>(),
                initialState = new { configuration = new { } }
            }), "institution/university.yaml");
            var bindings = Document(JsonSerializer.Serialize(new
            {
                institution = "university",
                version = "1.0.0",
                deployment = new { name = "test" },
                bindings = new Dictionary<string, object> { ["registry"] = new { provider = "keycloak", realm } }
            }), "institution/university.bindings.yaml");
            return Task.FromResult(new SourceLoadResult(new SourceBundle(definition, bindings), []));
        }
    }

    /// <summary>
    /// A hand-built Campus matching institution/campus.yaml's post-University shape: the three
    /// still-owned message-driven resources, plus a real IRegistrar dependency (no "registry"
    /// resource entry at all - it's inherited, not owned) instead of FourResourceSource's owned
    /// "registry".
    /// </summary>
    private sealed class CampusUnderUniversitySource(string suffix) : IDefinitionSource
    {
        public Task<SourceLoadResult> LoadAsync(DefinitionSourceRequest request, CancellationToken ct = default)
        {
            var definition = Document(JsonSerializer.Serialize(new
            {
                descriptor = new { id = "campus", name = "Campus", version = "1.0.0", description = "Test fixture campus under a university." },
                dependencies = new[] { new { contract = "IRegistrar", reason = "Test dependency on the University's shared registry." } },
                domains = Array.Empty<object>(),
                capabilities = Array.Empty<object>(),
                organizations = Array.Empty<object>(),
                roles = Array.Empty<object>(),
                resources = new[]
                {
                    new { id = "archive", name = "Archive", description = "Test archive resource.", type = "archive", ownership = "owned" },
                    new { id = "library", name = "Library", description = "Test library resource.", type = "knowledge", ownership = "owned" },
                    new { id = "post-office", name = "Post Office", description = "Test post office resource.", type = "post", ownership = "owned" },
                },
                workflows = Array.Empty<object>(),
                policies = Array.Empty<object>(),
                initialState = new { configuration = new { } }
            }), "institution/campus.yaml");
            var bindings = Document(JsonSerializer.Serialize(new
            {
                institution = "campus",
                version = "1.0.0",
                deployment = new { name = "test" },
                bindings = new Dictionary<string, object>
                {
                    ["archive"] = new { provider = "s3", bucket = "test-" + suffix },
                    ["library"] = new { provider = "mongodb", database = "test-" + suffix },
                    ["post-office"] = new { provider = "rabbitmq", vhost = "test-" + suffix },
                    ["IRegistrar"] = new { source = "university.registry" },
                }
            }), "institution/campus.bindings.yaml");
            return Task.FromResult(new SourceLoadResult(new SourceBundle(definition, bindings), []));
        }
    }

    /// <summary>
    /// Demultiplexes by RequestId - required because RabbitMQ fan-out delivers every published
    /// message to every subscriber of a reference, so any test using the shared
    /// ProvisioningPost.RequestReference()/ResultReference() may see completions meant for a
    /// concurrently-running test, not just its own. A single shared TaskCompletionSource (this
    /// class's predecessor, ResultConsumer) accepted whichever message arrived first regardless
    /// of RequestId - safe only when exactly one test ever used these references at a time, which
    /// stopped being true once this file grew enough tests to run concurrently against them.
    /// </summary>
    private sealed class MultiResultConsumer : MessageConsumerBase<InstitutionDeploymentCompleted>
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TaskCompletionSource<InstitutionDeploymentCompleted>> _pending = new();

        public override IPostContract Contract => ProvisioningPost.ResultReference().Contract;

        public TaskCompletionSource<InstitutionDeploymentCompleted> Expect(Guid requestId) =>
            _pending.GetOrAdd(requestId, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));

        public override Task ConsumeAsync(InstitutionDeploymentCompleted message, IPostContext context, CancellationToken ct = default)
        {
            Expect(message.RequestId).TrySetResult(message);
            return Task.CompletedTask;
        }
    }
}

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")))
            Skip = "Set PROVISIONING_TEST_RABBITMQ to an amqp:// connection string for an isolated broker.";
    }
}

public sealed class AllStage6ProvidersFactAttribute : FactAttribute
{
    public AllStage6ProvidersFactAttribute()
    {
        var missing = new[] { "PROVISIONING_TEST_RABBITMQ", "PROVISIONING_TEST_MONGO", "PROVISIONING_TEST_KEYCLOAK", "PROVISIONING_TEST_S3" }
            .Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToArray();
        if (missing.Length > 0) Skip = "Set " + string.Join(", ", missing) + " for an isolated broker/server of each kind.";
    }
}
