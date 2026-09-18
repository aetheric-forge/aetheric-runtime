using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Providers;
using AethericForge.Runtime.Institutions.Campus;
using AethericForge.Runtime.Models.Post;
using AethericForge.Runtime.Providers.Post.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AethericForge.Runtime.Tests.Post;

public class CampusPostSubscriptionTests
{
    private sealed record TestMessage(string Value);

    private sealed class RecordingConsumer : IMessageConsumer<TestMessage>
    {
        public IPostContract Contract { get; } = new PostContract("test.message", "1", PostIntent.Event);
        public List<TestMessage> Messages { get; } = [];

        public Task ConsumeAsync(IPostEnvelope envelope, IPostContext context, CancellationToken ct = default)
            => ConsumeAsync((TestMessage)envelope.Payload, context, ct);

        public Task ConsumeAsync(TestMessage message, IPostContext context, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task AddPostSubscription_ActivatesTheSubscriptionAtHostedServiceStartup()
    {
        var provider = new InMemoryPostProvider("campus");
        var contract = new PostContract("test.message", "1", PostIntent.Event);
        var reference = new PostReference("campus", "tests/message", contract);
        var consumer = new RecordingConsumer();

        var services = new ServiceCollection();
        services.AddSingleton<IPostProvider>(provider);
        services.AddPostSubscription(reference, consumer);
        using var provider2 = services.BuildServiceProvider();

        var hostedService = provider2.GetRequiredService<Microsoft.Extensions.Hosting.IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        // Not subscribed until the hosted service actually starts - proves this isn't accidentally
        // eager at registration time.
        await provider.PublishAsync(new PostEnvelope<TestMessage>(reference, new TestMessage("hello"), new PostMetadata()));

        Assert.Equal(["hello"], consumer.Messages.Select(x => x.Value));
    }

    [Fact]
    public async Task AddPostSubscription_MultipleSubscriptions_AreAllActivatedByOneHostedServiceInstance()
    {
        var provider = new InMemoryPostProvider("campus");
        var contract = new PostContract("test.message", "1", PostIntent.Event);
        var referenceA = new PostReference("campus", "tests/a", contract);
        var referenceB = new PostReference("campus", "tests/b", contract);
        var consumerA = new RecordingConsumer();
        var consumerB = new RecordingConsumer();

        var services = new ServiceCollection();
        services.AddSingleton<IPostProvider>(provider);
        services.AddPostSubscription(referenceA, consumerA);
        services.AddPostSubscription(referenceB, consumerB);
        using var provider2 = services.BuildServiceProvider();

        // Registering twice must not register the hosted service twice - otherwise each
        // subscription would be activated once per hosted-service instance and messages would be
        // delivered more than once.
        var hostedServices = provider2.GetServices<Microsoft.Extensions.Hosting.IHostedService>().ToList();
        Assert.Single(hostedServices);

        await hostedServices[0].StartAsync(CancellationToken.None);
        await provider.PublishAsync(new PostEnvelope<TestMessage>(referenceA, new TestMessage("a"), new PostMetadata()));
        await provider.PublishAsync(new PostEnvelope<TestMessage>(referenceB, new TestMessage("b"), new PostMetadata()));

        Assert.Equal(["a"], consumerA.Messages.Select(x => x.Value));
        Assert.Equal(["b"], consumerB.Messages.Select(x => x.Value));
    }
}
