using System.Collections.Immutable;
using System.Text.Json;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Simulation;
using Aetheric.Provisioning.Workbench;
using Aetheric.Provisioning.Workbench.Redis;
using Aetheric.Provisioning.Worker;
using StackExchange.Redis;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class WorkbenchParentCapabilityTests
{
    private static ParentContext Parent => new("https://example.test/owner", new string('a', 40))
        { Capabilities = ImmutableDictionary<string, string>.Empty.Add("IWorkbench", "owner.workbench") };

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("{\"Version\":2,\"Environment\":\"prod\",\"Institution\":\"owner\",\"Resource\":\"workspace\",\"Stage\":\"stage\"}")]
    public void Missing_or_unsupported_locations_are_rejected(string json) => Assert.False(WorkbenchParentLocation.TryParse(json, out _));

    [Fact]
    public async Task Missing_credentials_or_malformed_location_are_unavailable_without_connecting()
    {
        foreach (var location in new[] { "invalid", JsonSerializer.Serialize(new WorkbenchParentLocation(1, "prod", "owner", "workspace", "stage")) })
        {
            var parent = new ParentIdentity(Parent.Id, Parent.Revision, new Dictionary<string, string> { ["IWorkbench"] = location });
            var resolver = InstitutionDeploymentRequestConsumer.BuildResolver(parent, new Dictionary<string, RootCredentialPayload>());
            Assert.False(await resolver.IsAvailableAsync(Parent, "IWorkbench", "owner.workbench", default));
        }
        var malformed = new ParentIdentity(Parent.Id, Parent.Revision, new Dictionary<string, string> { ["IWorkbench"] = "invalid" });
        var withCredentials = InstitutionDeploymentRequestConsumer.BuildResolver(malformed,
            new Dictionary<string, RootCredentialPayload> { ["redis"] = new("invalid.example", 6379, null, "not-a-secret") });
        Assert.False(await withCredentials.IsAvailableAsync(Parent, "IWorkbench", "owner.workbench", default));
    }

    [RedisFact]
    public async Task Existing_registration_is_verified_without_changing_marker_or_draft_data()
    {
        var options = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("WORKBENCH_TEST_REDIS")!);
        using var redis = await ConnectionMultiplexer.ConnectAsync(options);
        var db = redis.GetDatabase();
        var stage = "test-parent-" + Guid.NewGuid().ToString("N");
        var location = new WorkbenchParentLocation(1, "integration", "owner", "workspace", stage);
        var markerKey = RedisWorkbenchBackend.RegistrationKey(stage);
        var draftKey = stage + ":data:existing";
        using var resolver = new RedisWorkbenchParentCapabilityResolver(db, location);
        try
        {
            // Populated legacy stage is not adopted or considered provisioned.
            await db.StringSetAsync(draftKey, "preserve-me");
            Assert.False(await resolver.IsAvailableAsync(Parent, "IWorkbench", "owner.workbench", default));
            Assert.False(await db.KeyExistsAsync(markerKey));
            await db.KeyDeleteAsync(draftKey);
            await new RedisWorkbenchBackend(db).EnsureAsync(new("integration", "owner", "workspace", stage), default);
            await db.StringSetAsync(draftKey, "preserve-me");
            var marker = await db.StringGetAsync(markerKey);
            Assert.True(await resolver.IsAvailableAsync(Parent, "IWorkbench", "owner.workbench", default));
            Assert.False(await resolver.IsAvailableAsync(Parent, "ILibrary", "owner.library", default));
            Assert.False(await resolver.IsAvailableAsync(Parent, "IWorkbench", "different.source", default));
            using var wrongOwner = new RedisWorkbenchParentCapabilityResolver(db, location with { Institution = "other" });
            Assert.False(await wrongOwner.IsAvailableAsync(Parent, "IWorkbench", "owner.workbench", default));
            Assert.Equal(marker, await db.StringGetAsync(markerKey));
            Assert.Equal("preserve-me", (string?)await db.StringGetAsync(draftKey));
            await db.KeyExpireAsync(markerKey, TimeSpan.FromMinutes(1));
            Assert.False(await resolver.IsAvailableAsync(Parent, "IWorkbench", "owner.workbench", default));
            await db.StringSetAsync(markerKey, "malformed");
            Assert.False(await resolver.IsAvailableAsync(Parent, "IWorkbench", "owner.workbench", default));
        }
        finally { await db.KeyDeleteAsync([markerKey, draftKey]); }
    }

    [RedisFact]
    public async Task Worker_wiring_uses_requested_redis_database_and_reports_failed_parent_in_engine()
    {
        var options = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("WORKBENCH_TEST_REDIS")!);
        using var redis = await ConnectionMultiplexer.ConnectAsync(options);
        var db = redis.GetDatabase();
        var (host, port) = options.EndPoints.Single() switch
        {
            System.Net.DnsEndPoint dns => (dns.Host, dns.Port),
            System.Net.IPEndPoint ip => (ip.Address.ToString(), ip.Port),
            _ => throw new InvalidOperationException("Unsupported test endpoint.")
        };
        var stage = "test-worker-" + Guid.NewGuid().ToString("N");
        var location = new WorkbenchParentLocation(1, "integration", "owner", "workspace", stage);
        var parent = new ParentIdentity(Parent.Id, Parent.Revision,
            new Dictionary<string, string> { ["IWorkbench"] = JsonSerializer.Serialize(location) });
        var credentials = new Dictionary<string, RootCredentialPayload>
        {
            ["redis"] = new(host, port, options.User, options.Password ?? "",
                Database: db.Database.ToString(), Scheme: options.Ssl ? "rediss" : "redis")
        };
        var resolver = InstitutionDeploymentRequestConsumer.BuildResolver(parent, credentials);
        var provenance = new SourceProvenance(Parent.Id, Parent.Revision, "definition.yaml", new string('b', 64));
        var plan = new ProvisioningPlanner([]).Plan(new(
            new InstitutionRequirements("decisions", "1.0.0", [], ["IWorkbench"]),
            new DeploymentBindings("decisions", "1.0.0", "integration", ImmutableDictionary<string, ResourceBinding>.Empty, Parent.Capabilities),
            Parent, provenance, provenance)).Plan!;
        var engine = new ProvisioningEngine([], resolver, new InMemoryRunStateStore(), new NoSecrets());
        try
        {
            Assert.False(await resolver.IsAvailableAsync(Parent, "IWorkbench", "owner.workbench", default));
            var missing = await engine.ExecuteAsync(plan);
            Assert.False(missing.Succeeded);
            Assert.Equal("parent.unavailable", Assert.Single(missing.Outcomes).Code);
            Assert.Equal("parent:IWorkbench", missing.Outcomes[0].StepId);
            await new RedisWorkbenchBackend(db).EnsureAsync(new("integration", "owner", "workspace", stage), default);
            Assert.True((await engine.ExecuteAsync(plan)).Succeeded);
            var deniedOptions = options.Clone();
            deniedOptions.Password = "intentionally-wrong-password";
            deniedOptions.AbortOnConnectFail = true; deniedOptions.ConnectRetry = 0; deniedOptions.ConnectTimeout = 1000;
            using var denied = new RedisWorkbenchParentCapabilityResolver(deniedOptions, location);
            var deniedResult = await new ProvisioningEngine([], denied, new InMemoryRunStateStore(), new NoSecrets()).ExecuteAsync(plan);
            Assert.False(deniedResult.Succeeded);
            Assert.Equal("operation.failed", Assert.Single(deniedResult.Outcomes).Code);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.IsAvailableAsync(Parent, "IWorkbench", "owner.workbench", cancelled.Token));
        }
        finally { (resolver as IDisposable)?.Dispose(); await db.KeyDeleteAsync(RedisWorkbenchBackend.RegistrationKey(stage)); }
    }
    private sealed class NoSecrets : ISecretStore
    {
        public Task<SecretReference> GetOrCreateAsync(string scope, string name, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> ReadAsync(SecretReference reference, CancellationToken ct) => throw new NotSupportedException();
    }

}
