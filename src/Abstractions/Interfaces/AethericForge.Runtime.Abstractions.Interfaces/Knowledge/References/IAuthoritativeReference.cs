using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.References;

public interface IAuthoritativeReference : IKnowledgeReference
{
    IKnowledgeAuthority Authority { get; }
    string Role { get; }
    string? Signature { get; }
}
