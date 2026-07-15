namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

public interface IKnowledgeDescriptor
{
    string Title { get; }
    string? Abstract { get; }
    string? Summary { get; }
    string? Description { get; }
}
