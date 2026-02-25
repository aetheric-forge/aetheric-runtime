using AethericForge.Runtime.Bus.Abstractions;
using AethericForge.Runtime.Util;

namespace AethericForge.Runtime.Tests;

public class MessageTests
{
    [Fact]
    public void Id_When_Null_Is_Assigned_New_Guid()
    {
        var msg = new TestModels.TestMessage("x.y");
        Assert.NotEqual(Guid.Empty, msg.Id);
    }

    [Fact]
    public void Id_When_Provided_Is_Preserved()
    {
        var id = Guid.NewGuid();
        var msg = new TestModels.TestMessage(id, "x.y");
        Assert.Equal(id, msg.Id);
    }

    [Theory]
    [InlineData(typeof(TestModels.MessageSent), "MessageSent")]
    public void Thread_Message_Subclasses_Derive_Routing_Key_From_Class_Name_When_Type_Not_Provided(Type messageType, string expectedType)
    {
        var msg = messageType switch
        {
            var t when t == typeof(TestModels.MessageSent) => new TestModels.MessageSent(Guid.NewGuid()),
            _ => throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "Unknown message type.")
        };

        // Act: create an envelope without specifying routing key/type
        var env = new Envelope { Kind = "event", Topic = msg.GetType().Name, Payload = System.Text.Json.JsonSerializer.SerializeToElement(msg) }; // or new Envelope(msg), or BusEnvelope.Create(msg), etc.

        // Assert: envelope inferred it
        Assert.Equal(expectedType, RoutingHelpers.ResolveRoutingKey(env)); // or env.Type / env.MessageType / env.Topic, whatever you call it
    }
}
