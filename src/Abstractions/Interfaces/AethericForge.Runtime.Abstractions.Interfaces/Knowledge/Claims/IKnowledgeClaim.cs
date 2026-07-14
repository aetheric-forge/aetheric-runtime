using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Claims;

public interface IKnowledgeClaim : IKnowledgeArtifact
{
    IIdentitySubject Asserter { get; }
    string ClaimType { get; }
    IKnowledgeObject Subject { get; }
    object? Statement { get; }
}
