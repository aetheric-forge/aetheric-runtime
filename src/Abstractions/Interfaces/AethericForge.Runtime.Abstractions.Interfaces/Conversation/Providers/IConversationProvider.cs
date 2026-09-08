using AethericForge.Runtime.Abstractions.Interfaces.Conversation.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Conversation.Consumers;
namespace AethericForge.Runtime.Abstractions.Interfaces.Conversation.Providers;

public interface IConversationProvider
{
    string Name { get; }
    // History ordering and pagination are owned by the adapter; enumeration must remain cancellable.
    IAsyncEnumerable<IConversationMessage> ReadMessagesAsync(IConversationReference conversation, CancellationToken ct = default);
    // Text is plain text. Thread identifiers are opaque and local to the conversation.
    Task<IConversationMessage> SendMessageAsync(IConversationReference conversation, string text,
        string operationId, string? threadId = null, CancellationToken ct = default);
    // Disposal stops this subscription. Delivery may repeat; consumers deduplicate event IDs.
    Task<IAsyncDisposable> SubscribeAsync(IConversationReference conversation, IConversationEventConsumer consumer, CancellationToken ct = default);
}
