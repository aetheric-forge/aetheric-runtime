namespace AethericForge.Runtime.Abstractions.Interfaces.Conversation.Primitives;

/// <summary>An opaque external address scoped to a provider and its workspace/account.</summary>
public interface IConversationReference
{
    string Provider { get; }
    string Scope { get; }
    string Id { get; }
}
