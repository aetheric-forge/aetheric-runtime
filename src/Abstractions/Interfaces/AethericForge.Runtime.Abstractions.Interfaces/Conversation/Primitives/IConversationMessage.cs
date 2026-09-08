namespace AethericForge.Runtime.Abstractions.Interfaces.Conversation.Primitives;

public interface IConversationMessage
{
    IConversationReference Conversation { get; }
    string Id { get; }
    string ParticipantId { get; }
    string Text { get; }
    string? ThreadId { get; }
    DateTimeOffset SentAt { get; }
    DateTimeOffset? EditedAt { get; }
    Uri? SourceUrl { get; }
}
