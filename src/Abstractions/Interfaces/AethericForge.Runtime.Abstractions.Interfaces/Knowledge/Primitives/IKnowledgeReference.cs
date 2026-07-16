namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

public interface IKnowledgeReference
{
    string Scheme { get; }
    string Kind { get; }
    string Name { get; }
    string Version { get; }
    int Revision { get; }
    string? ContentHash { get; }
}
