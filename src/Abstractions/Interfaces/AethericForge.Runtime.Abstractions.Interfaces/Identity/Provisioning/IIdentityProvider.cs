using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;

public interface IIdentityProvider
{
    string Name { get; }
    IdentityScheme Scheme { get; }

    Task<IIdentitySubject?> ResolveSubjectAsync(
        string subjectId,
        CancellationToken cancellationToken = default);

    Task<IPrincipalIdentity?> AuthenticateAsync(
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken = default);
}