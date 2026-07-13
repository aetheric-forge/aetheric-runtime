using AethericForge.Runtime.Abstractions.Interfaces.Identity.Primitives;

namespace AethericForge.Runtime.Models.Identity.Core;

public sealed record PrincipalIdentity : IPrincipalIdentity
{
    public PrincipalIdentity(
        IIdentitySubject subject,
        bool isAuthenticated = false,
        IEnumerable<IIdentityClaim>? claims = null)
    {
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        Scheme = subject.Scheme;
        IsAuthenticated = isAuthenticated;
        Claims = claims?.ToArray() ?? subject.Claims;
    }

    public IIdentitySubject Subject { get; }
    public IdentityScheme Scheme { get; }
    public bool IsAuthenticated { get; }
    public IReadOnlyCollection<IIdentityClaim> Claims { get; }
}
