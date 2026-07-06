using AethericForge.Runtime.Abstractions.Interfaces.Identity.Core;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;

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
