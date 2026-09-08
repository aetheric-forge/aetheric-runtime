using AethericForge.Runtime.Abstractions.Interfaces.Conversation.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Conversation.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Conversation.Consumers;

namespace AethericForge.Runtime.Models.Conversation;

public abstract class ConversationProvider : IConversationProvider
{
    protected ConversationProvider(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
    public string Name { get; }
    public IAsyncEnumerable<IConversationMessage> ReadMessagesAsync(IConversationReference conversation, CancellationToken ct = default)
    {
        Validate(conversation, ct);
        return ReadMessagesCoreAsync(conversation, ct);
    }
    public Task<IConversationMessage> SendMessageAsync(IConversationReference conversation, string text,
        string operationId, string? threadId = null, CancellationToken ct = default)
    {
        Validate(conversation, ct);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (threadId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        return SendMessageCoreAsync(conversation, text, operationId, threadId, ct);
    }
    protected abstract IAsyncEnumerable<IConversationMessage> ReadMessagesCoreAsync(IConversationReference conversation, CancellationToken ct);
    protected abstract Task<IConversationMessage> SendMessageCoreAsync(IConversationReference conversation, string text, string operationId, string? threadId, CancellationToken ct);

    public Task<IAsyncDisposable> SubscribeAsync(IConversationReference conversation, IConversationEventConsumer consumer, CancellationToken ct = default)
    {
        Validate(conversation, ct);
        ArgumentNullException.ThrowIfNull(consumer);
        return SubscribeCoreAsync(conversation, consumer, ct);
    }
    protected abstract Task<IAsyncDisposable> SubscribeCoreAsync(IConversationReference conversation, IConversationEventConsumer consumer, CancellationToken ct);
    private void Validate(IConversationReference reference, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.Scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference.Id);
        if (!string.Equals(Name, reference.Provider, StringComparison.Ordinal))
            throw new ArgumentException("Reference belongs to a different provider.", nameof(reference));
    }
}
