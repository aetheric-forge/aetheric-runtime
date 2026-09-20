using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Worker;
using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Models.Post;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class BootstrapCompletionMetadataTests
{
    // Metadata is an AMQP header/property projection, not a JSON-deserialized PostMetadata body.
    private static PostMetadata ReadMetadata(JsonNode node) => new(
        messageId: node["MessageId"]!.GetValue<string>(),
        correlationId: node["CorrelationId"]?.GetValue<string>(),
        causationId: node["CausationId"]?.GetValue<string>(),
        producedAtUtc: node["ProducedAtUtc"]!.GetValue<DateTimeOffset>(),
        attributes: node["Attributes"]!.Deserialize<Dictionary<string, string>>());

    [Fact]
    public async Task Consumer_preserves_profile_ids_on_a_failed_bootstrap_without_live_infrastructure()
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "BootstrapContractFixtures", "request.json")))!;
        var request = fixture["Payload"]!.Deserialize<InstitutionBootstrapRequested>()!;
        // Fail during pure parsing, before accessing any provider, checkpoint, or secret.
        request = request with { University = new InstitutionConfig("not: [valid yaml", "invalid") };
        var metadata = ReadMetadata(fixture["Metadata"]!);
        var post = new RecordingPost();
        var consumer = new InstitutionBootstrapRequestConsumer(post, new InstitutionYamlReader(),
            new UnusedState(), new UnusedSecrets());
        await consumer.ConsumeAsync(request, new Context(new PostEnvelope<InstitutionBootstrapRequested>(
            ProvisioningBootstrapPost.RequestReference(), request, metadata)));

        var completion = Assert.IsType<PostEnvelope<InstitutionBootstrapCompleted>>(Assert.Single(post.Published));
        Assert.Equal(request.RequestId, completion.Payload.RequestId);
        Assert.False(completion.Payload.Succeeded);
        Assert.Equal(BootstrapStepStatus.Failed, completion.Payload.Steps[0].Status);
        // The consumer now preflights (parses + statically plans) all four before executing any -
        // University's mutated YAML fails on a syntax error, and the other three - the fixture's
        // own "Example: X" placeholder text, deliberately not deployable content per the runtime
        // README - each independently fail their own preflight too (missing descriptor.id), so
        // every step reports its own real Failed verdict here, not a NotAttempted cascade. That's
        // the whole point of preflighting the envelope: surfacing every structural problem in one
        // round rather than only the first one discovered.
        Assert.All(completion.Payload.Steps.Skip(1), step =>
        {
            Assert.Equal(BootstrapStepStatus.Failed, step.Status);
            Assert.Contains("yaml.required: descriptor.id", Assert.Single(step.Issues));
        });
        Assert.Equal(metadata.MessageId, completion.Metadata.CausationId);
        Assert.Equal(metadata.CorrelationId, completion.Metadata.CorrelationId);
        Assert.Equal(metadata.Attributes.OrderBy(pair => pair.Key), completion.Metadata.Attributes.OrderBy(pair => pair.Key));
        Assert.NotEqual(metadata.MessageId, completion.Metadata.MessageId);
        Assert.Equal(completion.Payload.CompletedAtUtc, completion.Metadata.ProducedAtUtc);
    }

    private sealed class RecordingPost : IPostProvider
    {
        public string Name => ProvisioningPost.Domain;
        public List<IPostEnvelope> Published { get; } = [];
        public Task PublishAsync(IPostEnvelope envelope, CancellationToken ct = default)
        { Published.Add(envelope); return Task.CompletedTask; }
        public Task SubscribeAsync(IPostReference reference, IMessageConsumer consumer, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class Context(IPostEnvelope envelope) : IPostContext
    {
        public IPostEnvelope Envelope => envelope;
        public IReadOnlyDictionary<string, string> Attributes => envelope.Metadata.Attributes;
        public Task PublishAsync<TMessage>(IPostReference reference, TMessage message,
            IPostMetadata? metadata = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class UnusedState : IRunStateStore
    {
        public Task<RunState?> ReadAsync(string planId, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveAsync(RunState state, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class UnusedSecrets : ISecretStore
    {
        public Task<SecretReference> GetOrCreateAsync(string scope, string name, CancellationToken ct) => throw new NotSupportedException();
        public Task<string> ReadAsync(SecretReference reference, CancellationToken ct) => throw new NotSupportedException();
    }
}
