using AethericForge.Runtime.Abstractions.Interfaces.Conversation.Primitives;

namespace AethericForge.Runtime.Models.Conversation;

public sealed record ConversationReference : IConversationReference
{
    public ConversationReference(string provider, string scope, string id)
    {
        Provider = Required(provider, nameof(provider));
        Scope = Required(scope, nameof(scope));
        Id = Required(id, nameof(id));
    }
    public string Provider { get; }
    public string Scope { get; }
    public string Id { get; }
    private static string Required(string value, string parameter) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Value is required.", parameter);
}
