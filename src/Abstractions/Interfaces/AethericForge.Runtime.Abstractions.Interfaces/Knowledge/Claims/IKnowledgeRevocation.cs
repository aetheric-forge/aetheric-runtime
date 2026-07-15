using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Claims;

public interface IKnowledgeRevocation : IKnowledgeArtifact
{
    IIdentitySubject Asserter { get; }
    IKnowledgeReference Target { get; }
    string? Reason { get; }
}
