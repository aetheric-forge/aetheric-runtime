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
    private static ResourceRequirement Owned(string resourceId) => new(resourceId, resourceId, "test", "owned");
    private static InstitutionRequirements Requirements(string id, string? ownedResourceId, params string[] parentContracts) =>
        new(id, "1.0.0", ownedResourceId is null ? [] : [Owned(ownedResourceId)], [.. parentContracts]);
    private static DeploymentBindings Bindings(string id, string? resourceId, string? settingKey, string? settingValue) =>
        new(id, "1.0.0", "test",
            resourceId is null ? ImmutableDictionary<string, ResourceBinding>.Empty
                : ImmutableDictionary<string, ResourceBinding>.Empty.Add(resourceId,
                    new("test", settingKey is null ? ImmutableDictionary<string, string>.Empty
                        : ImmutableDictionary<string, string>.Empty.Add(settingKey, settingValue!), ImmutableDictionary<string, SecretReference>.Empty)),
            ImmutableDictionary<string, string>.Empty);

    // Registry, Archive, and Post Office all resolve to a binding setting (realm/bucket/vhost) on
    // the owning level itself.
    private static InstitutionBootstrapRequestConsumer.DeployedLevel Level(string id, string? ownedResourceId, string? settingKey, string? settingValue) =>
        new(new($"https://example.test/{id}", "0000000000000000000000000000000000000000", "institution.yaml", new string('0', 64)),
            Requirements(id, ownedResourceId), Bindings(id, ownedResourceId, settingKey, settingValue));

    // Library resolves to the owning level's own institution id instead (see ResolveLocation's
    // own doc comment) - no binding setting involved at all.
    private static InstitutionBootstrapRequestConsumer.DeployedLevel LibraryLevel(string id, bool ownsLibrary) =>
        new(new($"https://example.test/{id}", "0000000000000000000000000000000000000000", "institution.yaml", new string('0', 64)),
            Requirements(id, ownsLibrary ? "library" : null), Bindings(id, null, null, null));

    [Fact]
    public void Resolves_to_the_nearest_owning_ancestor_not_the_furthest()
    {
        var chain = new[] { Level("university", "registry", "realm", "university-realm"), Level("campus", "registry", "realm", "campus-realm") };
        Assert.Equal("campus-realm", InstitutionBootstrapRequestConsumer.ResolveLocation(chain, "IRegistrar"));
    }

    [Fact]
    public void Skips_an_inheriting_ancestor_to_find_the_one_that_actually_owns_it()
    {
        var chain = new[] { Level("university", "registry", "realm", "university-realm"), Level("campus", null, null, null) };
        Assert.Equal("university-realm", InstitutionBootstrapRequestConsumer.ResolveLocation(chain, "IRegistrar"));
    }

    [Fact]
    public void No_owning_ancestor_anywhere_in_the_chain_resolves_to_null()
    {
        var chain = new[] { Level("campus", null, null, null) };
        Assert.Null(InstitutionBootstrapRequestConsumer.ResolveLocation(chain, "IRegistrar"));
    }

    [Fact]
    public void An_unrecognized_contract_resolves_to_null()
    {
        var chain = new[] { Level("university", "registry", "realm", "university-realm") };
        Assert.Null(InstitutionBootstrapRequestConsumer.ResolveLocation(chain, "ISomethingElse"));
    }

    [Fact]
    public void A_level_with_no_dependency_needs_no_parent_identity_at_all()
    {
        var chain = new[] { Level("university", "registry", "realm", "university-realm") };
        Assert.Null(InstitutionBootstrapRequestConsumer.BuildParentIdentity(chain, Requirements("campus", null)));
    }

    [Fact]
    public void A_dependent_level_gets_a_parent_identity_carrying_the_resolved_realm()
    {
        var chain = new[] { Level("university", "registry", "realm", "university-realm"), Level("campus", "registry", "realm", "campus-realm") };
        var parent = InstitutionBootstrapRequestConsumer.BuildParentIdentity(chain, Requirements("faculty", null, "IRegistrar"));
        Assert.NotNull(parent);
        Assert.Equal("campus-realm", parent.ResourceLocations["IRegistrar"]);
        // The parent identity (repository/revision) is the immediate org-chart parent (last in
        // the chain), even when the realm itself was resolved further up - the two are separate
        // concerns (audit identity vs. which live resource actually answers the check).
        Assert.Equal(chain[^1].Provenance.Repository, parent.Repository);
    }

    [Fact]
    public void Library_resolves_to_the_nearest_owning_ancestors_own_institution_id_not_a_setting()
    {
        var chain = new[] { LibraryLevel("university", ownsLibrary: false), LibraryLevel("campus", ownsLibrary: true) };
        Assert.Equal("campus", InstitutionBootstrapRequestConsumer.ResolveLocation(chain, "ILibrary"));
    }

    [Fact]
    public void Different_resources_at_the_same_level_resolve_independently()
    {
        // Faculty inherits Registry from University but Library from Campus (which privatized
        // its own) - the two contracts must not interfere with each other's resolution.
        var chain = new[]
        {
            Level("university", "registry", "realm", "university-realm"),
            new InstitutionBootstrapRequestConsumer.DeployedLevel(
                new("https://example.test/campus", "0000000000000000000000000000000000000000", "institution.yaml", new string('0', 64)),
                Requirements("campus", "library"), Bindings("campus", "library", null, null)),
        };
        var parent = InstitutionBootstrapRequestConsumer.BuildParentIdentity(chain, Requirements("faculty", null, "IRegistrar", "ILibrary"));
        Assert.NotNull(parent);
        Assert.Equal("university-realm", parent.ResourceLocations["IRegistrar"]);
        Assert.Equal("campus", parent.ResourceLocations["ILibrary"]);
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

    /// <summary>
    /// Decisions Institution's real, eventual dependency set (IArchive/ILibrary/IPostOffice/
    /// IRegistrar, matching fixtures/decisions/decisions-institution.yaml's own shape) resolved
    /// against the actual campus.yaml-shaped hierarchy: University owns only Registry; Campus
    /// owns Archive/Library/Post Office directly and inherits Registry from University;
    /// Administration Faculty and Decisions own nothing and inherit all four - IRegistrar
    /// resolving all the way to University, the other three resolving to Campus. Proves every
    /// contract resolves independently against its own nearest owner, not a single "the parent"
    /// shortcut.
    /// </summary>
    [AllStage6ProvidersFact]
    public async Task Worker_consumer_resolves_all_four_resources_across_the_whole_hierarchy()
    {
        var connection = Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!;
        await using var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, connection);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var universityRealm = "test-university-" + suffix;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "aetheric-provisioning-bootstrap-test-" + Guid.NewGuid().ToString("N"));
        var consumer = BuildConsumer(tempDirectory);
        await postProvider.SubscribeAsync(ProvisioningBootstrapPost.RequestReference(), consumer);
        var results = new MultiCompletionConsumer();
        await postProvider.SubscribeAsync(ProvisioningBootstrapPost.ResultReference(), results);

        try
        {
            var requestId = Guid.NewGuid();
            var wait = results.Expect(requestId);
            var university = Fixture("university", new Dictionary<string, object> { ["registry"] = new { provider = "keycloak", realm = universityRealm } });
            var campus = Fixture("campus", new Dictionary<string, object>
            {
                ["archive"] = new { provider = "s3", bucket = "test-" + suffix },
                ["library"] = new { provider = "mongodb", database = "test-" + suffix },
                ["post-office"] = new { provider = "rabbitmq", vhost = "test-" + suffix },
            }, "IRegistrar");
            var administrationFaculty = Fixture("administration-faculty", new Dictionary<string, object>(),
                "IRegistrar", "IArchive", "ILibrary", "IPostOffice");
            var decisions = Fixture("decisions", new Dictionary<string, object>(),
                "IRegistrar", "IArchive", "ILibrary", "IPostOffice");

            var request = new InstitutionBootstrapRequested(
                requestId, university, campus, administrationFaculty, decisions,
                new Dictionary<string, RootCredentialPayload>
                {
                    ["keycloak"] = KeycloakCredential(), ["rabbitmq"] = RabbitMqCredential(),
                    ["mongo"] = MongoCredential(), ["s3"] = S3Credential(),
                },
                DateTimeOffset.UtcNow);
            await postProvider.PublishAsync(new PostEnvelope<InstitutionBootstrapRequested>(
                ProvisioningBootstrapPost.RequestReference(), request, new PostMetadata()));

            var completed = await wait.Envelope.Task.WaitAsync(TimeSpan.FromSeconds(60));
            var payload = completed.Payload;

            Assert.True(payload.Succeeded, string.Join("; ", payload.Steps.SelectMany(s => s.Issues)));
            Assert.All(payload.Steps, step => Assert.Equal(BootstrapStepStatus.Succeeded, step.Status));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Campus additionally privatizes Registry (owns it directly instead of inheriting from
    /// University) while still owning Archive/Library/Post Office as before - Decisions is given
    /// no dependencies at all here specifically so the whole envelope still succeeds even though
    /// Campus's own realm is a *different* realm than University's, proving the integration path
    /// (a non-root level owning Registry in addition to its other resources, with a descendant's
    /// dependency still resolving and succeeding) actually works end to end. This does not by
    /// itself prove nearest-owning-ancestor picked Campus specifically rather than University -
    /// both realms genuinely exist here, so a live success alone can't distinguish "resolved
    /// correctly" from "resolved to the wrong-but-also-real ancestor". That distinction is what
    /// InstitutionBootstrapResolverTests.Different_resources_at_the_same_level_resolve_independently
    /// (pure, no live infra) actually asserts on the resolved value itself.
    /// </summary>
    [AllStage6ProvidersFact]
    public async Task Worker_consumer_resolves_a_privatized_campus_registry_independently_of_its_other_three_resources()
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
            var university = Fixture("university", new Dictionary<string, object> { ["registry"] = new { provider = "keycloak", realm = universityRealm } });
            var campus = Fixture("campus", new Dictionary<string, object>
            {
                ["registry"] = new { provider = "keycloak", realm = campusRealm },
                ["archive"] = new { provider = "s3", bucket = "test-" + suffix },
                ["library"] = new { provider = "mongodb", database = "test-" + suffix },
                ["post-office"] = new { provider = "rabbitmq", vhost = "test-" + suffix },
            });
            var administrationFaculty = Fixture("administration-faculty", new Dictionary<string, object>(),
                "IRegistrar", "IArchive", "ILibrary", "IPostOffice");
            var decisions = Fixture("decisions", new Dictionary<string, object>());

            var request = new InstitutionBootstrapRequested(
                requestId, university, campus, administrationFaculty, decisions,
                new Dictionary<string, RootCredentialPayload>
                {
                    ["keycloak"] = KeycloakCredential(), ["rabbitmq"] = RabbitMqCredential(),
                    ["mongo"] = MongoCredential(), ["s3"] = S3Credential(),
                },
                DateTimeOffset.UtcNow);
            await postProvider.PublishAsync(new PostEnvelope<InstitutionBootstrapRequested>(
                ProvisioningBootstrapPost.RequestReference(), request, new PostMetadata()));

            var completed = await wait.Envelope.Task.WaitAsync(TimeSpan.FromSeconds(60));
            var payload = completed.Payload;

            Assert.True(payload.Succeeded, string.Join("; ", payload.Steps.SelectMany(s => s.Issues)));
            Assert.All(payload.Steps, step => Assert.Equal(BootstrapStepStatus.Succeeded, step.Status));
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

    private static RootCredentialPayload RabbitMqCredential()
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

    private static RootCredentialPayload S3Credential()
    {
        var uri = new Uri(Environment.GetEnvironmentVariable("PROVISIONING_TEST_S3")!);
        var userInfo = uri.UserInfo.Split(':', 2);
        return new RootCredentialPayload(uri.Host, uri.Port, Uri.UnescapeDataString(userInfo[0]), Uri.UnescapeDataString(userInfo[1]), Scheme: uri.Scheme);
    }

    private static InstitutionConfig University(string realm) =>
        Fixture("university", new Dictionary<string, object> { ["registry"] = new { provider = "keycloak", realm } });
    private static InstitutionConfig DependentLevel(string id) => Fixture(id, new Dictionary<string, object>(), "IRegistrar");
    private static InstitutionConfig OwnedRegistryLevel(string id, string realm) =>
        Fixture(id, new Dictionary<string, object> { ["registry"] = new { provider = "keycloak", realm } });

    // Maps each owned resource id (institution/campus.yaml's own ids) to the parent contract it
    // satisfies - used to fill in the "resources" section's "type" field for a synthesized fixture.
    private static readonly Dictionary<string, string> ResourceType = new()
    {
        ["registry"] = "identity", ["archive"] = "archive", ["library"] = "knowledge", ["post-office"] = "post",
    };

    /// <summary>
    /// A general-purpose institution fixture: ownedResources maps a resource id to its binding
    /// settings object (e.g. {provider="keycloak", realm}); dependencyContracts lists whichever
    /// parent contracts this level inherits (source is a placeholder string - only the contract
    /// name matters to the nearest-owning-ancestor resolver, which never reads it back).
    /// </summary>
    private static InstitutionConfig Fixture(string id, Dictionary<string, object> ownedResources, params string[] dependencyContracts)
    {
        var definition = JsonSerializer.Serialize(new
        {
            descriptor = new { id, name = id, version = "1.0.0", description = "Test fixture." },
            dependencies = dependencyContracts.Select(contract => new { contract, reason = "Test dependency on the nearest owning ancestor." }).ToArray(),
            domains = Array.Empty<object>(),
            capabilities = Array.Empty<object>(),
            organizations = Array.Empty<object>(),
            roles = Array.Empty<object>(),
            resources = ownedResources.Keys.Select(resourceId => new
            {
                id = resourceId, name = resourceId, description = "Test resource.", type = ResourceType[resourceId], ownership = "owned",
            }).ToArray(),
            workflows = Array.Empty<object>(),
            policies = Array.Empty<object>(),
            initialState = new { configuration = new { } },
        });
        var bindingsMap = new Dictionary<string, object>(ownedResources);
        foreach (var contract in dependencyContracts) bindingsMap[contract] = new { source = "nearest-owning-ancestor" };
        var bindings = JsonSerializer.Serialize(new
        {
            institution = id,
            version = "1.0.0",
            deployment = new { name = "test" },
            bindings = bindingsMap,
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
