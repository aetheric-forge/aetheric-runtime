using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Models.Post;
using AethericForge.Runtime.Providers.Post.RabbitMq;
using Xunit;

namespace AethericForge.Runtime.IntegrationTests;

public sealed class RabbitMqPostProviderTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Published_post_is_consumed_with_payload_and_correlation()
    {
        var domain = EnvironmentConfiguration.Get("AF_E2E_RABBITMQ_DOMAIN", "e2e");
        await using var provider = new RabbitMqPostProvider(
            domain,
            EnvironmentConfiguration.Require("AF_E2E_RABBITMQ_URI"));

        var runId = Guid.NewGuid().ToString("N");
        var reference = new PostReference(
            domain,
            $"integration.{runId}",
            new PostContract("round-trip", "1", PostIntent.Event));
        var consumed = new TaskCompletionSource<TestMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new TestConsumer(reference.Contract, consumed);

        await provider.SubscribeAsync(reference, consumer);
        await provider.PublishAsync(new PostEnvelope<TestMessage>(
            reference,
            new TestMessage(runId),
            new PostMetadata(correlationId: runId)));

        var received = await consumed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(runId, received.RunId);
    }

    private sealed record TestMessage(string RunId);

    private sealed class TestConsumer(
        IPostContract contract,
        TaskCompletionSource<TestMessage> consumed)
        : IMessageConsumer<TestMessage>
    {
        public IPostContract Contract => contract;

        public Task ConsumeAsync(
            IPostEnvelope envelope,
            IPostContext context,
            CancellationToken ct = default)
        {
            consumed.TrySetResult(Assert.IsType<TestMessage>(envelope.Payload));
            return Task.CompletedTask;
        }

        public Task ConsumeAsync(
            TestMessage message,
            IPostContext context,
            CancellationToken ct = default)
        {
            consumed.TrySetResult(message);
            return Task.CompletedTask;
        }
    }
}
