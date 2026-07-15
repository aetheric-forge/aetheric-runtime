using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;

public interface IKnowledgeAuthority
{
    IIdentitySubject Identity { get; }
    string Context { get; }
}
