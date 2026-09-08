using AethericForge.Runtime.Abstractions.Interfaces.Conversation.Primitives;
namespace AethericForge.Runtime.Abstractions.Interfaces.Conversation.Consumers;

public interface IConversationEventConsumer
{
    Task ConsumeAsync(IConversationEvent conversationEvent, CancellationToken ct = default);
}
