using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Compositions;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Models.Knowledge.Compositions;

public sealed record KnowledgeConstituent(
    IKnowledgeReference Reference,
    string Role,
    int? Order = null) : IKnowledgeConstituent;
