using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Models.Post;
using AethericForge.Runtime.Providers.Post.InMemory;
using AethericForge.Runtime.Services.Post;

namespace AethericForge.Runtime.Tests.Post;

public class PostTests
{
    [Fact]
    public async Task PostService_Publishes_To_Subscribers_On_Matching_Reference()
    {
        var provider = new InMemoryPostProvider("campus");
        var service = new PostService([provider]);
        var contract = new PostContract("student.enrolled", "1", PostIntent.Event);
        var reference = new PostReference("campus", "students/enrolled", contract);
        var consumer = new RecordingConsumer<StudentEnrolled>(contract);

        await service.SubscribeAsync(reference, consumer);
        await service.PublishAsync(reference, new StudentEnrolled("student-1"));

        Assert.Equal(["student-1"], consumer.Messages.Select(x => x.StudentId));
        Assert.Single(consumer.Contexts);
        Assert.Same(reference, consumer.Contexts[0].Envelope.Reference);
    }

    [Fact]
    public async Task InMemoryPostProvider_Uses_Exact_Reference_Matching()
    {
        var provider = new InMemoryPostProvider("campus");
        var service = new PostService([provider]);
        var contract = new PostContract("student.enrolled", "1", PostIntent.Event);
        var subscribed = new PostReference(
            "campus",
            "students/enrolled",
            contract,
            new Dictionary<string, string> { ["tenant"] = "one" });
        var unmatched = new PostReference(
            "campus",
            "students/enrolled",
            contract,
            new Dictionary<string, string> { ["tenant"] = "two" });
        var consumer = new RecordingConsumer<StudentEnrolled>(contract);

        await service.SubscribeAsync(subscribed, consumer);
        await service.PublishAsync(unmatched, new StudentEnrolled("student-1"));

        Assert.Empty(consumer.Messages);
    }

    [Fact]
    public async Task InMemoryPostContext_Can_Publish_Followup_Message()
    {
        var provider = new InMemoryPostProvider("campus");
        var service = new PostService([provider]);
        var receivedContract = new PostContract("student.enrolled", "1", PostIntent.Event);
        var followupContract = new PostContract("student.welcomed", "1", PostIntent.Event);
        var receivedReference = new PostReference("campus", "students/enrolled", receivedContract);
        var followupReference = new PostReference("campus", "students/welcomed", followupContract);
        var consumer = new PublishingConsumer<StudentEnrolled, StudentWelcomed>(
            receivedContract,
            followupReference,
            message => new StudentWelcomed(message.StudentId));
        var followupConsumer = new RecordingConsumer<StudentWelcomed>(followupContract);
        var metadata = new PostMetadata("message-1");

        await service.SubscribeAsync(receivedReference, consumer);
        await service.SubscribeAsync(followupReference, followupConsumer);
        await service.PublishAsync(receivedReference, new StudentEnrolled("student-1"), metadata);

        Assert.Equal(["student-1"], followupConsumer.Messages.Select(x => x.StudentId));
        Assert.Equal("message-1", followupConsumer.Contexts[0].Envelope.Metadata.CorrelationId);
        Assert.Equal("message-1", followupConsumer.Contexts[0].Envelope.Metadata.CausationId);
    }

    [Fact]
    public async Task PostService_Requires_A_Provider_For_The_Requested_Domain()
    {
        var service = new PostService([new InMemoryPostProvider("campus")]);
        var reference = new PostReference(
            "archive",
            "students/enrolled",
            new PostContract("student.enrolled", "1", PostIntent.Event));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.PublishAsync(reference, new StudentEnrolled("student-1")));
    }

    [Fact]
    public async Task InMemoryPostProvider_Rejects_References_For_Other_Domains()
    {
        var provider = new InMemoryPostProvider("campus");
        var reference = new PostReference(
            "archive",
            "students/enrolled",
            new PostContract("student.enrolled", "1", PostIntent.Event));
        var envelope = new PostEnvelope<StudentEnrolled>(
            reference,
            new StudentEnrolled("student-1"),
            new PostMetadata());

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.PublishAsync(envelope));
    }

    [Fact]
    public void PostMetadata_Normalizes_Values_And_Attributes()
    {
        var producedAt = new DateTimeOffset(2026, 7, 11, 9, 30, 0, TimeSpan.FromHours(-6));
        var metadata = new PostMetadata(
            " message-1 ",
            " correlation-1 ",
            " causation-1 ",
            producedAt,
            new Dictionary<string, string> { [" tenant "] = "one" });

        Assert.Equal("message-1", metadata.MessageId);
        Assert.Equal("correlation-1", metadata.CorrelationId);
        Assert.Equal("causation-1", metadata.CausationId);
        Assert.Equal(TimeSpan.Zero, metadata.ProducedAtUtc.Offset);
        Assert.Equal("one", metadata.Attributes["tenant"]);
    }

    private sealed record StudentEnrolled(string StudentId);
    private sealed record StudentWelcomed(string StudentId);

    private sealed class PublishingConsumer<TReceived, TPublished> : IMessageConsumer<TReceived>
    {
        private readonly IPostReference _publishReference;
        private readonly Func<TReceived, TPublished> _createMessage;

        public PublishingConsumer(
            IPostContract contract,
            IPostReference publishReference,
            Func<TReceived, TPublished> createMessage)
        {
            Contract = contract;
            _publishReference = publishReference;
            _createMessage = createMessage;
        }

        public IPostContract Contract { get; }

        public Task ConsumeAsync(
            IPostEnvelope envelope,
            IPostContext context,
            CancellationToken ct = default)
        {
            return envelope is IPostEnvelope<TReceived> typedEnvelope
                ? ConsumeAsync(typedEnvelope.Payload, context, ct)
                : ConsumeAsync((TReceived)envelope.Payload, context, ct);
        }

        public Task ConsumeAsync(
            TReceived message,
            IPostContext context,
            CancellationToken ct = default)
        {
            return context.PublishAsync(_publishReference, _createMessage(message), ct: ct);
        }
    }

    private sealed class RecordingConsumer<TMessage> : IMessageConsumer<TMessage>
    {
        public RecordingConsumer(IPostContract contract)
        {
            Contract = contract;
        }

        public IPostContract Contract { get; }
        public List<TMessage> Messages { get; } = [];
        public List<IPostContext> Contexts { get; } = [];

        public Task ConsumeAsync(
            IPostEnvelope envelope,
            IPostContext context,
            CancellationToken ct = default)
        {
            return envelope is IPostEnvelope<TMessage> typedEnvelope
                ? ConsumeAsync(typedEnvelope.Payload, context, ct)
                : ConsumeAsync((TMessage)envelope.Payload, context, ct);
        }

        public Task ConsumeAsync(
            TMessage message,
            IPostContext context,
            CancellationToken ct = default)
        {
            Messages.Add(message);
            Contexts.Add(context);
            return Task.CompletedTask;
        }
    }
}
