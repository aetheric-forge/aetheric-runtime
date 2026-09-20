using System.Collections.Immutable;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Persistence;
using Aetheric.Provisioning.Worker;
using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Models.Post;
using AethericForge.Runtime.Providers.Post.RabbitMq;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class InlineSourceDocumentsTests
{
    [Fact]
    public void Build_produces_matching_content_hashes_and_synthetic_but_valid_provenance()
    {
        var config = new InstitutionConfig("descriptor text", "bindings text");
        var bundle = InlineSourceDocuments.Build("university", config);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(config.DefinitionYaml))),
            bundle.Definition.Provenance.ContentHash);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(config.BindingsYaml))),
            bundle.Bindings.Provenance.ContentHash);

        foreach (var document in new[] { bundle.Definition, bundle.Bindings })
        {
            Assert.True(Uri.TryCreate(document.Provenance.Repository, UriKind.Absolute, out var uri) && uri.Scheme == "https");
            Assert.Matches("^[0-9a-f]{40}$", document.Provenance.Commit);
            Assert.Matches("^[0-9a-f]{64}$", document.Provenance.ContentHash);
        }

        // Definition and Bindings must share the same Repository/Commit - InstitutionYamlReader.Read
        // rejects "source.mixed_revision" otherwise.
        Assert.Equal(bundle.Definition.Provenance.Repository, bundle.Bindings.Provenance.Repository);
        Assert.Equal(bundle.Definition.Provenance.Commit, bundle.Bindings.Provenance.Commit);
    }
}

public sealed class InstitutionBootstrapResolverTests
{
    private static ResourceRequirement OwnedRegistry => new("registry", "Registry", "identity", "owned");
    private static DeploymentBindings Bindings(string realm) => new("x", "1.0.0", "test",
        ImmutableDictionary<string, ResourceBinding>.Empty.Add("registry",
            new("keycloak", ImmutableDictionary<string, string>.Empty.Add("realm", realm), ImmutableDictionary<string, SecretReference>.Empty)),
        ImmutableDictionary<string, string>.Empty);
    private static InstitutionRequirements Requirements(bool ownsRegistry, params string[] parentContracts) =>
        new("x", "1.0.0", ownsRegistry ? [OwnedRegistry] : [], [.. parentContracts]);
    private static InstitutionBootstrapRequestConsumer.DeployedLevel Level(bool ownsRegistry, string realm) =>
        new(new("https://example.test/fixture", "0000000000000000000000000000000000000000", "institution.yaml", new string('0', 64)),
            Requirements(ownsRegistry), Bindings(realm));

    [Fact]
    public void Resolves_to_the_nearest_owning_ancestor_not_the_furthest()
    {
        var chain = new[] { Level(ownsRegistry: true, "university-realm"), Level(ownsRegistry: true, "campus-realm") };
        Assert.Equal("campus-realm", InstitutionBootstrapRequestConsumer.ResolveRegistryRealm(chain));
    }

    [Fact]
    public void Skips_an_inheriting_ancestor_to_find_the_one_that_actually_owns_it()
    {
        var chain = new[] { Level(ownsRegistry: true, "university-realm"), Level(ownsRegistry: false, "unused") };
        Assert.Equal("university-realm", InstitutionBootstrapRequestConsumer.ResolveRegistryRealm(chain));
    }

    [Fact]
    public void No_owning_ancestor_anywhere_in_the_chain_resolves_to_null()
    {
        var chain = new[] { Level(ownsRegistry: false, "unused") };
        Assert.Null(InstitutionBootstrapRequestConsumer.ResolveRegistryRealm(chain));
    }

    [Fact]
    public void A_level_with_no_dependency_needs_no_parent_identity_at_all()
    {
        var chain = new[] { Level(ownsRegistry: true, "university-realm") };
        Assert.Null(InstitutionBootstrapRequestConsumer.BuildParentIdentity(chain, Requirements(ownsRegistry: false)));
    }

    [Fact]
    public void A_dependent_level_gets_a_parent_identity_carrying_the_resolved_realm()
    {
        var chain = new[] { Level(ownsRegistry: true, "university-realm"), Level(ownsRegistry: true, "campus-realm") };
        var parent = InstitutionBootstrapRequestConsumer.BuildParentIdentity(chain, Requirements(ownsRegistry: false, "IRegistrar"));
        Assert.NotNull(parent);
        Assert.Equal("campus-realm", parent.RegistryRealm);
        // The parent identity (repository/revision) is the immediate org-chart parent (last in
        // the chain), even when the realm itself was resolved further up - the two are separate
        // concerns (audit identity vs. which live resource actually answers the check).
        Assert.Equal(chain[^1].Provenance.Repository, parent.Repository);
    }
}

public sealed class InstitutionBootstrapWorkerTests
{
    private static InstitutionBootstrapRequestConsumer BuildConsumer(string tempDirectory) => new(
        new RabbitMqPostProvider(ProvisioningPost.Domain,
            Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!),
        new InstitutionYamlReader(),
        new FileRunStateStore(Path.Combine(tempDirectory, "run-state")),
        new EncryptedFileSecretStore(Path.Combine(tempDirectory, "secrets"), RandomNumberGenerator.GetBytes(32)));

    [AllStage6ProvidersFact]
    public async Task Worker_consumer_bootstraps_the_whole_hierarchy_when_everything_inherits_from_university()
    {
        var connection = Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!;
        await using var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, connection);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var realm = "test-university-" + suffix;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "aetheric-provisioning-bootstrap-test-" + Guid.NewGuid().ToString("N"));
        var consumer = BuildConsumer(tempDirectory);
        await postProvider.SubscribeAsync(ProvisioningBootstrapPost.RequestReference(), consumer);
        var results = new MultiCompletionConsumer();
        await postProvider.SubscribeAsync(ProvisioningBootstrapPost.ResultReference(), results);

        try
        {
            var requestId = Guid.NewGuid();
            var wait = results.Expect(requestId);
            var request = new InstitutionBootstrapRequested(
                requestId,
                University(realm),
                DependentLevel("campus"),
                DependentLevel("administration-faculty"),
                DependentLevel("decisions"),
                new Dictionary<string, RootCredentialPayload> { ["keycloak"] = KeycloakCredential() },
                DateTimeOffset.UtcNow);
            var initiatorMessageId = Guid.NewGuid().ToString("N");
            await postProvider.PublishAsync(new PostEnvelope<InstitutionBootstrapRequested>(
                ProvisioningBootstrapPost.RequestReference(), request, new PostMetadata(messageId: initiatorMessageId)));

            var completed = await wait.Envelope.Task.WaitAsync(TimeSpan.FromSeconds(45));
            var payload = completed.Payload;

            Assert.True(payload.Succeeded, string.Join("; ", payload.Steps.SelectMany(s => s.Issues)));
            Assert.Equal(4, payload.Steps.Length);
            Assert.All(payload.Steps, step => Assert.Equal(BootstrapStepStatus.Succeeded, step.Status));
            Assert.Equal(["University", "Campus", "AdministrationFaculty", "Decisions"], payload.Steps.Select(s => s.Step));

            // "the initiation recorded as the causation."
            Assert.Equal(initiatorMessageId, completed.Metadata.CausationId);
            Assert.Equal(initiatorMessageId, completed.Metadata.CorrelationId);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [AllStage6ProvidersFact]
    public async Task Worker_consumer_provisions_a_privatized_campus_registry_and_faculty_still_resolves()
    {
        var connection = Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!;
        await using var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, connection);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var universityRealm = "test-university-" + suffix;
        var campusRealm = "test-campus-" + suffix;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "aetheric-provisioning-bootstrap-test-" + Guid.NewGuid().ToString("N"));
        var consumer = BuildConsumer(tempDirectory);
        await postProvider.SubscribeAsync(ProvisioningBootstrapPost.RequestReference(), consumer);
        var results = new MultiCompletionConsumer();
        await postProvider.SubscribeAsync(ProvisioningBootstrapPost.ResultReference(), results);

        try
        {
            var requestId = Guid.NewGuid();
            var wait = results.Expect(requestId);
            var request = new InstitutionBootstrapRequested(
                requestId,
                University(universityRealm),
                OwnedRegistryLevel("campus", campusRealm),
                DependentLevel("administration-faculty"),
                DependentLevel("decisions"),
                new Dictionary<string, RootCredentialPayload> { ["keycloak"] = KeycloakCredential() },
                DateTimeOffset.UtcNow);
            await postProvider.PublishAsync(new PostEnvelope<InstitutionBootstrapRequested>(
                ProvisioningBootstrapPost.RequestReference(), request, new PostMetadata()));

            var completed = await wait.Envelope.Task.WaitAsync(TimeSpan.FromSeconds(45));
            var payload = completed.Payload;

            Assert.True(payload.Succeeded, string.Join("; ", payload.Steps.SelectMany(s => s.Issues)));
            Assert.All(payload.Steps, step => Assert.Equal(BootstrapStepStatus.Succeeded, step.Status));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [AllStage6ProvidersFact]
    public async Task Worker_consumer_reports_remaining_steps_not_attempted_when_university_fails()
    {
        var connection = Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!;
        await using var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, connection);
        var tempDirectory = Path.Combine(Path.GetTempPath(), "aetheric-provisioning-bootstrap-test-" + Guid.NewGuid().ToString("N"));
        var consumer = BuildConsumer(tempDirectory);
        await postProvider.SubscribeAsync(ProvisioningBootstrapPost.RequestReference(), consumer);
        var results = new MultiCompletionConsumer();
        await postProvider.SubscribeAsync(ProvisioningBootstrapPost.ResultReference(), results);

        try
        {
            var requestId = Guid.NewGuid();
            var wait = results.Expect(requestId);
            // No "keycloak" credential at all - University's own owned registry resource has no
            // provider registered for it, so University itself fails cleanly.
            var request = new InstitutionBootstrapRequested(
                requestId,
                University("unused-realm"),
                DependentLevel("campus"),
                DependentLevel("administration-faculty"),
                DependentLevel("decisions"),
                new Dictionary<string, RootCredentialPayload>(),
                DateTimeOffset.UtcNow);
            await postProvider.PublishAsync(new PostEnvelope<InstitutionBootstrapRequested>(
                ProvisioningBootstrapPost.RequestReference(), request, new PostMetadata()));

            var completed = await wait.Envelope.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var payload = completed.Payload;

            Assert.False(payload.Succeeded);
            Assert.Equal(BootstrapStepStatus.Failed, payload.Steps[0].Status);
            Assert.All(payload.Steps.Skip(1), step => Assert.Equal(BootstrapStepStatus.NotAttempted, step.Status));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static RootCredentialPayload KeycloakCredential()
    {
        var uri = new Uri(Environment.GetEnvironmentVariable("PROVISIONING_TEST_KEYCLOAK")!);
        var userInfo = uri.UserInfo.Split(':', 2);
        return new RootCredentialPayload(uri.Host, uri.Port, Uri.UnescapeDataString(userInfo[0]), Uri.UnescapeDataString(userInfo[1]), Scheme: uri.Scheme);
    }

    private static InstitutionConfig University(string realm) => Level("university", ownsRegistry: true, realm: realm, dependsOnRegistrar: false);
    private static InstitutionConfig DependentLevel(string id) => Level(id, ownsRegistry: false, realm: null, dependsOnRegistrar: true);
    private static InstitutionConfig OwnedRegistryLevel(string id, string realm) => Level(id, ownsRegistry: true, realm: realm, dependsOnRegistrar: false);

    private static InstitutionConfig Level(string id, bool ownsRegistry, string? realm, bool dependsOnRegistrar)
    {
        var definition = JsonSerializer.Serialize(new
        {
            descriptor = new { id, name = id, version = "1.0.0", description = "Test fixture." },
            dependencies = dependsOnRegistrar
                ? new object[] { new { contract = "IRegistrar", reason = "Test dependency on the nearest owning ancestor's registry." } }
                : Array.Empty<object>(),
            domains = Array.Empty<object>(),
            capabilities = Array.Empty<object>(),
            organizations = Array.Empty<object>(),
            roles = Array.Empty<object>(),
            resources = ownsRegistry
                ? new object[] { new { id = "registry", name = "Registry", description = "Test registry.", type = "identity", ownership = "owned" } }
                : Array.Empty<object>(),
            workflows = Array.Empty<object>(),
            policies = Array.Empty<object>(),
            initialState = new { configuration = new { } },
        });
        var bindings = JsonSerializer.Serialize(new
        {
            institution = id,
            version = "1.0.0",
            deployment = new { name = "test" },
            bindings = ownsRegistry
                ? new Dictionary<string, object> { ["registry"] = new { provider = "keycloak", realm } }
                : dependsOnRegistrar
                    ? new Dictionary<string, object> { ["IRegistrar"] = new { source = "nearest-owning-ancestor.registry" } }
                    : new Dictionary<string, object>(),
        });
        return new InstitutionConfig(definition, bindings);
    }

    /// <summary>Demultiplexes InstitutionBootstrapCompleted by RequestId - same rationale as InstitutionDeploymentWorkerTests's MultiResultConsumer (RabbitMQ fan-out delivers every published message to every subscriber).</summary>
    private sealed class MultiCompletionConsumer : MessageConsumerBase<InstitutionBootstrapCompleted>
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, PendingCompletion> _pending = new();

        public override IPostContract Contract => ProvisioningBootstrapPost.ResultReference().Contract;

        public PendingCompletion Expect(Guid requestId) => _pending.GetOrAdd(requestId, _ => new PendingCompletion());

        public override Task ConsumeAsync(InstitutionBootstrapCompleted message, IPostContext context, CancellationToken ct = default)
        {
            Expect(message.RequestId).Envelope.TrySetResult(new CompletedEnvelope(message, context.Envelope.Metadata));
            return Task.CompletedTask;
        }

        public sealed class PendingCompletion
        {
            public TaskCompletionSource<CompletedEnvelope> Envelope { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed record CompletedEnvelope(InstitutionBootstrapCompleted Payload, IPostMetadata Metadata);
}
