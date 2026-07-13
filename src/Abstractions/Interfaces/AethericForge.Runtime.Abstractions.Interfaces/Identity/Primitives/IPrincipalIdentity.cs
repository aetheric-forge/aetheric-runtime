namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Primitives;

public interface IPrincipalIdentity : IIdentitySubject
{
    IIdentitySubject Subject { get; }
    bool IsAuthenticated { get; }
}
