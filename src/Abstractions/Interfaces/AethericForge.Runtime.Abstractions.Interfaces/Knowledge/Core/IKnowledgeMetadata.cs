namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Core;

public interface IKnowledgeMetadata
{
    string Set { get; }
    string Kind { get; }
    string Name { get; }
    string Version { get; }
    int Revision { get; }
    string? Description { get; }
    string? ContentHash { get; }
    DateTimeOffset CreatedAtUtc { get; }
    DateTimeOffset UpdatedAtUtc { get; }
}
