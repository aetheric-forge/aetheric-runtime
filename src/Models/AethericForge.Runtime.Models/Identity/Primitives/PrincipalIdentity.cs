using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Models.Identity.Primitives;

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

    public string SubjectId => Subject.SubjectId;
    public string? DisplayName => Subject.DisplayName;
    public IdentityState State => Subject.State;
}
