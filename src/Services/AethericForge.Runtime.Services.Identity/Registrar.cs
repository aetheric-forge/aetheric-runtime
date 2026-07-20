using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Services.Identity;

public sealed class Registrar(IIdentityService identityService) : IRegistrar
{
    private readonly IIdentityService _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));

    public Task<IPrincipalIdentity?> AuthenticateAsync(
        IdentityScheme scheme,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        return _identityService.AuthenticateAsync(scheme, credentials, ct);
    }

    public Task<IIdentitySubject?> ResolveSubjectAsync(
        IdentityScheme scheme,
        string subjectId,
        CancellationToken ct = default)
    {
        return _identityService.ResolveSubjectAsync(scheme, subjectId, ct);
    }

    public Task<IPrincipalIdentity?> ResolvePrincipalAsync(
        IIdentitySubject subject,
        CancellationToken ct = default)
    {
        return _identityService.ResolvePrincipalAsync(subject, ct);
    }
}
