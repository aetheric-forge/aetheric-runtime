namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Primitives;

public interface IPrincipalIdentity
{
    IIdentitySubject Subject { get; }
    IdentityScheme Scheme { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<IIdentityClaim> Claims { get; }
}
