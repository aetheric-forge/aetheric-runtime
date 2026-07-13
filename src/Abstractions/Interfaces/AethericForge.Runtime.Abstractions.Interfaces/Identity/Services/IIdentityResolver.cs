using AethericForge.Runtime.Abstractions.Interfaces.Identity.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;

public interface IIdentityResolver
{
    Task<IIdentitySubject?> ResolveSubjectAsync(
        IdentityScheme scheme,
        string subjectId,
        CancellationToken cancellationToken = default);

    Task<IPrincipalIdentity?> ResolvePrincipalAsync(
        IIdentitySubject subject,
        CancellationToken cancellationToken = default);
}
