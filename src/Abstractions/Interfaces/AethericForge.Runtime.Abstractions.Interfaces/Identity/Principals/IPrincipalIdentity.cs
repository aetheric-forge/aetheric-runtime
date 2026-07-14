using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;

public interface IPrincipalIdentity : IIdentitySubject
{
    IIdentitySubject Subject { get; }
    bool IsAuthenticated { get; }
}