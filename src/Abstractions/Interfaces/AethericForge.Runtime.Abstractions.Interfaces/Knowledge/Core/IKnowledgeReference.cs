namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Core;

public interface IKnowledgeReference
{
    string Set { get; }
    string Kind { get; }
    string Name { get; }
    string Version { get; }
    int Revision { get; }
    string? ContentHash { get; }
}
