using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Compositions;

public interface IKnowledgeComposition : IKnowledgeArtifact
{
    IReadOnlyCollection<IKnowledgeConstituent> Constituents { get; }
}
