using AethericForge.Runtime.Abstractions.Interfaces.Post;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Post.Primitives;
using AethericForge.Runtime.Models.Post;
using AethericForge.Runtime.Providers.Post.RabbitMq;
using Testcontainers.RabbitMq;
using Xunit;

namespace AethericForge.Runtime.Providers.Post.RabbitMq.Tests;

public class RabbitMqPostProviderTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder().Build();

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbitMqContainer.DisposeAsync();
    }

    [Fact]
    public async Task RabbitMqPostProvider_Publishes_And_Subscribes()
    {
        var connectionString = _rabbitMqContainer.GetConnectionString();
        await using var provider = new RabbitMqPostProvider("campus", connectionString);
        
        var contract = new PostContract("student.enrolled", "1", PostIntent.Event);
        var reference = new PostReference("campus", "students/enrolled", contract);
        var consumer = new RecordingConsumer<StudentEnrolled>(contract);

        await provider.SubscribeAsync(reference, consumer);
        
        var message = new StudentEnrolled("student-1");
        var envelope = new PostEnvelope<StudentEnrolled>(reference, message, new PostMetadata());
        
        await provider.PublishAsync(envelope);

        // Wait for message to be processed
        for (int i = 0; i < 50 && consumer.Messages.Count == 0; i++)
        {
            await Task.Delay(100);
        }

        Assert.Single(consumer.Messages);
        Assert.Equal("student-1", consumer.Messages[0].StudentId);
    }

    [Fact]
    public async Task RabbitMqPostProvider_Supports_Followup_Messages_Via_Context()
    {
        var connectionString = _rabbitMqContainer.GetConnectionString();
        await using var provider = new RabbitMqPostProvider("campus", connectionString);

        var enrolledContract = new PostContract("student.enrolled", "1", PostIntent.Event);
        var welcomedContract = new PostContract("student.welcomed", "1", PostIntent.Event);
        
        var enrolledReference = new PostReference("campus", "students/enrolled", enrolledContract);
        var welcomedReference = new PostReference("campus", "students/welcomed", welcomedContract);

        var followupConsumer = new RecordingConsumer<StudentWelcomed>(welcomedContract);
        var initialConsumer = new PublishingConsumer<StudentEnrolled, StudentWelcomed>(
            enrolledContract,
            welcomedReference,
            msg => new StudentWelcomed(msg.StudentId));

        await provider.SubscribeAsync(enrolledReference, initialConsumer);
        await provider.SubscribeAsync(welcomedReference, followupConsumer);

        var metadata = new PostMetadata(messageId: "msg-123");
        var envelope = new PostEnvelope<StudentEnrolled>(enrolledReference, new StudentEnrolled("student-2"), metadata);
        
        await provider.PublishAsync(envelope);

        // Wait for followup message
        for (int i = 0; i < 50 && followupConsumer.Messages.Count == 0; i++)
        {
            await Task.Delay(100);
        }

        Assert.Single(followupConsumer.Messages);
        Assert.Equal("student-2", followupConsumer.Messages[0].StudentId);
        Assert.Equal("msg-123", followupConsumer.Contexts[0].Envelope.Metadata.CorrelationId);
        Assert.Equal("msg-123", followupConsumer.Contexts[0].Envelope.Metadata.CausationId);
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

        public Task ConsumeAsync(IPostEnvelope envelope, IPostContext context, CancellationToken ct = default)
        {
            var message = envelope is IPostEnvelope<TReceived> typed ? typed.Payload : (TReceived)envelope.Payload;
            return ConsumeAsync(message, context, ct);
        }

        public Task ConsumeAsync(TReceived message, IPostContext context, CancellationToken ct = default)
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
            if (envelope is IPostEnvelope<TMessage> typedEnvelope)
            {
                return ConsumeAsync(typedEnvelope.Payload, context, ct);
            }
            
            if (envelope.Payload is TMessage message)
            {
                return ConsumeAsync(message, context, ct);
            }
            
            return Task.CompletedTask;
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
