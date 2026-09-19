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
/// CampusDeploymentRequested drives the worker's consumer through the actual plan pipeline
/// (ProvisioningReview/Planner/Engine, not a mock), and the correct CampusDeploymentCompleted
/// comes back. One test proves the still-correct provider.unsupported failure for a request that
/// supplies no root credentials; the other proves a real success - the post-office resource
/// actually gets created (vhost/user/permissions), verified independently via the management API.
/// </summary>
public sealed class CampusDeploymentWorkerTests
{
    private static CampusDeploymentRequestConsumer BuildConsumer(IPostProvider postProvider, IDefinitionSource source, string tempDirectory) =>
        new(postProvider, source, new InstitutionYamlReader(), new NoParentCapabilityResolver(),
            new FileRunStateStore(Path.Combine(tempDirectory, "run-state")),
            new EncryptedFileSecretStore(Path.Combine(tempDirectory, "secrets"), RandomNumberGenerator.GetBytes(32)));

    [RabbitMqFact]
    public async Task Worker_consumer_reports_provider_unsupported_when_no_credentials_are_supplied()
    {
        var connection = Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!;
        await using var postProvider = new RabbitMqPostProvider(ProvisioningPost.Domain, connection);

        var tempDirectory = Path.Combine(Path.GetTempPath(), "aetheric-provisioning-worker-test-" + Guid.NewGuid().ToString("N"));
        var consumer = BuildConsumer(postProvider, new FixtureSource(), tempDirectory);

        var resultReceived = new TaskCompletionSource<CampusDeploymentCompleted>(TaskCreationOptions.RunContinuationsAsynchronously);
        await postProvider.SubscribeAsync(ProvisioningPost.ResultReference(), new ResultConsumer(resultReceived));
        await postProvider.SubscribeAsync(ProvisioningPost.RequestReference(), consumer);

        try
        {
            var request = new CampusDeploymentRequested(
                Guid.NewGuid(),
                "https://github.com/aetheric-forge/aetheric-runtime",
                "main",
                "institution/campus.yaml",
                "institution/campus.bindings.yaml",
                new Dictionary<string, RootCredentialPayload>(),
                DateTimeOffset.UtcNow);
            var envelope = new PostEnvelope<CampusDeploymentRequested>(
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

        var resultReceived = new TaskCompletionSource<CampusDeploymentCompleted>(TaskCreationOptions.RunContinuationsAsynchronously);
        await postProvider.SubscribeAsync(ProvisioningPost.ResultReference(), new ResultConsumer(resultReceived));
        await postProvider.SubscribeAsync(ProvisioningPost.RequestReference(), consumer);

        var management = ManagementCredential();
        using var managementClient = new HttpClient { BaseAddress = new Uri($"http://{management.Host}:{management.Port}/") };
        managementClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{management.Username}:{management.Password}")));

        try
        {
            var request = new CampusDeploymentRequested(
                Guid.NewGuid(),
                "https://example.test/fixture",
                "0000000000000000000000000000000000000000",
                "institution/campus.yaml",
                "institution/campus.bindings.yaml",
                new Dictionary<string, RootCredentialPayload> { ["rabbitmq"] = management },
                DateTimeOffset.UtcNow);
            var envelope = new PostEnvelope<CampusDeploymentRequested>(
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

    private static RootCredentialPayload ManagementCredential()
    {
        var amqp = new Uri(Environment.GetEnvironmentVariable("PROVISIONING_TEST_RABBITMQ")!);
        var userInfo = amqp.UserInfo.Split(':', 2);
        // RabbitMQ's management plugin listens on a fixed port independent of the AMQP port.
        return new RootCredentialPayload(amqp.Host, 15672, Uri.UnescapeDataString(userInfo[0]), Uri.UnescapeDataString(userInfo[1]));
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

    private sealed class ResultConsumer(TaskCompletionSource<CampusDeploymentCompleted> completion)
        : MessageConsumerBase<CampusDeploymentCompleted>
    {
        public override IPostContract Contract => ProvisioningPost.ResultReference().Contract;

        public override Task ConsumeAsync(CampusDeploymentCompleted message, IPostContext context, CancellationToken ct = default)
        {
            completion.TrySetResult(message);
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
