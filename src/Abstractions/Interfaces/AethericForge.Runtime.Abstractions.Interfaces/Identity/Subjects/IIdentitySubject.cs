using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

public interface IIdentitySubject
{
    string SubjectId { get; }
    IdentityScheme Scheme { get; }
    string? DisplayName { get; }
    IdentityState State { get; }
    IReadOnlyCollection<IIdentityClaim> Claims { get; }
}