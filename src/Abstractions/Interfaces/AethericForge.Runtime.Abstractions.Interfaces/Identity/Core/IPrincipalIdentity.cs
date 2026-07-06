namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Core;

public interface IPrincipalIdentity
{
    IIdentitySubject Subject { get; }
    IdentityScheme Scheme { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<IIdentityClaim> Claims { get; }
}
