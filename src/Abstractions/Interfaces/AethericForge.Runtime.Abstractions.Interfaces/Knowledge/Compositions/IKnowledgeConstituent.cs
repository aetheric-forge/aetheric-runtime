using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Compositions;

public interface IKnowledgeConstituent
{
    IKnowledgeReference Reference { get; }
    string Role { get; }
    int? Order { get; }
}
