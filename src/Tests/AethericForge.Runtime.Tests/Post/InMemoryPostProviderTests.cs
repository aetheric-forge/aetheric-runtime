using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Models.Post;
using AethericForge.Runtime.Providers.Post.InMemory;
using Xunit;

namespace AethericForge.Runtime.Tests.Post;

public class InMemoryPostProviderTests
{
    private readonly InMemoryPostProvider _provider;
    private const string Domain = "test-domain";

    public InMemoryPostProviderTests()
    {
        _provider = new InMemoryPostProvider(Domain);
    }

    [Fact]
    public async Task PublishAndSubscribe_WorkCorrectly()
    {
        // Arrange
        var contract = new PostContract("test.message", "1", PostIntent.Event);
        var reference = new PostReference(Domain, "address", contract);
        var consumer = new TestConsumer(contract);
        var message = new TestMessage("Hello");
        var envelope = new PostEnvelope<TestMessage>(reference, message, new PostMetadata());

        await _provider.SubscribeAsync(reference, consumer);

        // Act
        await _provider.PublishAsync(envelope);

        // Assert
        Assert.Single(consumer.Received);
        Assert.Equal("Hello", ((TestMessage)consumer.Received[0].Payload).Content);
    }

    [Fact]
    public async Task Subscribe_Throws_WhenContractMismatch()
    {
        // Arrange
        var refContract = new PostContract("test.message", "1", PostIntent.Event);
        var consContract = new PostContract("other.message", "1", PostIntent.Event);
        var reference = new PostReference(Domain, "address", refContract);
        var consumer = new TestConsumer(consContract);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.SubscribeAsync(reference, consumer));
    }

    private record TestMessage(string Content);

    private class TestConsumer : IMessageConsumer
    {
        public TestConsumer(IPostContract contract) => Contract = contract;
        public IPostContract Contract { get; }
        public List<IPostEnvelope> Received { get; } = [];

        public Task ConsumeAsync(IPostEnvelope envelope, IPostContext context, CancellationToken ct = default)
        {
            Received.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
