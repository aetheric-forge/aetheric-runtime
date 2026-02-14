using AethericForge.Runtime.Model.Messages;
using AethericForge.Runtime.Model.Messages.Threads;

namespace AethericForge.Runtime.Tests;

public class MessageTests
{
    private sealed class TestMessage(
        Guid? id = null,
        string? type = null,
        DateTime? timestamp = null,
        Guid? causationId = null,
        Guid? correlationId = null
    ) : Message(id, type, timestamp, causationId, correlationId)
    {
    }

    private sealed class TestPayloadMessage(
        object payload,
        Guid? id = null,
        string? type = null,
        DateTime? timestamp = null,
        Guid? causationId = null,
        Guid? correlationId = null
    ) : Message<object>(payload, type, id, timestamp, causationId, correlationId)
    {
    }

    [Fact]
    public void Id_When_Null_Is_Assigned_New_Guid()
    {
        var msg = new TestMessage(id: null, type: "x.y");
        Assert.NotEqual(Guid.Empty, msg.Id);
    }

    [Fact]
    public void Id_When_Provided_Is_Preserved()
    {
        var id = Guid.NewGuid();
        var msg = new TestMessage(id: id, type: "x.y");
        Assert.Equal(id, msg.Id);
    }

    [Fact]
    public void Timestamp_When_Null_Defaults_To_UtcNow_Close_To_Now()
    {
        var before = DateTime.UtcNow;
        var msg = new TestMessage(type: "x.y", timestamp: null);
        var after = DateTime.UtcNow;

        Assert.True(msg.Timestamp.Kind == DateTimeKind.Utc, "Timestamp should be UTC");
        Assert.True(msg.Timestamp >= before, $"Timestamp should be >= {before:o} but was {msg.Timestamp:o}");
        Assert.True(msg.Timestamp <= after, $"Timestamp should be <= {after:o} but was {msg.Timestamp:o}");
    }

    [Fact]
    public void Timestamp_When_Provided_Is_Preserved()
    {
        var ts = new DateTime(2030, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        var msg = new TestMessage(type: "x.y", timestamp: ts);

        Assert.Equal(ts, msg.Timestamp);
    }

    [Fact]
    public void Type_When_Provided_Is_Preserved()
    {
        var msg = new TestMessage(type: "alpha.beta");
        Assert.Equal("alpha.beta", msg.Type);
    }

    [Fact]
    public void Type_When_Null_Is_Derived_From_Runtime_Type()
    {
        // TestMessage -> "test.message"
        var msg = new TestMessage(type: null);
        Assert.Equal("test.message", msg.Type);
    }

    [Fact]
    public void Causation_And_Correlation_Ids_Are_Preserved()
    {
        var causation = Guid.NewGuid();
        var correlation = Guid.NewGuid();

        var msg = new TestMessage(
            type: "x.y",
            causationId: causation,
            correlationId: correlation
        );

        Assert.Equal(causation, msg.CausationId);
        Assert.Equal(correlation, msg.CorrelationId);
    }

    [Fact]
    public void MessageT_Payload_Is_Exposed_And_Cannot_Be_Null()
    {
        var payload = new object();
        var msg = new TestPayloadMessage(payload, type: "x.y");

        Assert.Same(payload, msg.Payload);

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new TestPayloadMessage(payload: null!, type: "x.y");
        });
    }

    [Theory]
    [InlineData(typeof(DomainCreated), "domain.created")]
    [InlineData(typeof(DomainUpdated), "domain.updated")]
    [InlineData(typeof(StoryCreated), "story.created")]
    [InlineData(typeof(StoryUpdated), "story.updated")]
    [InlineData(typeof(SagaCreated), "saga.created")]
    [InlineData(typeof(SagaUpdated), "saga.updated")]
    public void Thread_Message_Subclasses_Derive_Routing_Key_From_Class_Name_When_Type_Not_Provided(Type messageType, string expectedType)
    {
        Message msg = messageType switch
        {
            var t when t == typeof(DomainCreated) => TestModels.DomainCreated(),
            var t when t == typeof(DomainUpdated) => TestModels.DomainUpdated(),
            var t when t == typeof(StoryCreated) => TestModels.StoryCreated(),
            var t when t == typeof(StoryUpdated) => TestModels.StoryUpdated(),
            var t when t == typeof(SagaCreated) => TestModels.SagaCreated(),
            var t when t == typeof(SagaUpdated) => TestModels.SagaUpdated(),
            _ => throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "Unknown message type.")
        };

        Assert.Equal(expectedType, msg.Type);
    }
}
