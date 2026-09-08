namespace AethericForge.Runtime.Abstractions.Interfaces.Conversation.Primitives;

public enum ConversationEventKind { MessageCreated, MessageUpdated, MessageDeleted }

/// <summary>Event identity is scoped to Conversation. Deletes may carry no message body.</summary>
public interface IConversationEvent
{
    string EventId { get; }
    IConversationReference Conversation { get; }
    ConversationEventKind Kind { get; }
    string MessageId { get; }
    IConversationMessage? Message { get; }
    DateTimeOffset OccurredAt { get; }
    string? OriginOperationId { get; }
}
