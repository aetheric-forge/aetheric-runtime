using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Services.Registry;

public sealed class RegistryService(IIdentityService identityService) : IRegistryService
{
    public IIdentityService Identity { get; } = identityService ?? throw new ArgumentNullException(nameof(identityService));

    public Task<IPrincipalIdentity?> AuthenticateAsync(
        IdentityScheme scheme,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken = default)
    {
        return Identity.AuthenticateAsync(scheme, credentials, cancellationToken);
    }

    public Task<IIdentitySubject?> ResolveSubjectAsync(
        IdentityScheme scheme,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        return Identity.ResolveSubjectAsync(scheme, subjectId, cancellationToken);
    }

    public Task<IPrincipalIdentity?> ResolvePrincipalAsync(
        IIdentitySubject subject,
        CancellationToken cancellationToken = default)
    {
        return Identity.ResolvePrincipalAsync(subject, cancellationToken);
    }
}
