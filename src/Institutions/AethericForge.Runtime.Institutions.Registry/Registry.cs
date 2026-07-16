using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Institutions;
using AethericForge.Runtime.Services.Registry;

namespace AethericForge.Runtime.Institutions.Registry;

public sealed class Registry(IRegistryContext context, IRegistryService registryService) 
    : InstitutionBase(context), IRegistry
{
    private readonly IRegistryService _registryService = 
        registryService ?? throw new ArgumentNullException(nameof(registryService));

    public new IRegistryContext Context => (IRegistryContext)base.Context;

    public Task<IPrincipalIdentity?> AuthenticateAsync(
        IdentityScheme scheme,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        return _registryService.AuthenticateAsync(scheme, credentials, ct);
    }

    public Task<IIdentitySubject?> ResolveSubjectAsync(
        IdentityScheme scheme,
        string subjectId,
        CancellationToken ct = default)
    {
        return _registryService.ResolveSubjectAsync(scheme, subjectId, ct);
    }

    public Task<IPrincipalIdentity?> ResolvePrincipalAsync(
        IIdentitySubject subject,
        CancellationToken ct = default)
    {
        return _registryService.ResolvePrincipalAsync(subject, ct);
    }
}
