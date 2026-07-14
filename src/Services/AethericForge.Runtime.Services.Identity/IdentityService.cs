using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Lifecycle;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Provisioning;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Services.Identity;

public sealed class IdentityService : IIdentityService, IAuthenticationService, IIdentityResolver, IIdentityLifecycleService
{
    private readonly IReadOnlyDictionary<IdentityScheme, IIdentityProvider> _providers;
    private readonly IIdentityLifecycleService _lifecycleService;

    public IdentityService(
        IEnumerable<IIdentityProvider> providers,
        IIdentityLifecycleService lifecycleService)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(lifecycleService);

        _providers = providers.ToDictionary(p => p.Scheme);
        _lifecycleService = lifecycleService;
    }

    public Task<IPrincipalIdentity?> AuthenticateAsync(
        IdentityScheme scheme,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken = default)
    {
        return GetProvider(scheme).AuthenticateAsync(credentials, cancellationToken);
    }

    public Task<IIdentitySubject?> ResolveSubjectAsync(
        IdentityScheme scheme,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        return GetProvider(scheme).ResolveSubjectAsync(subjectId, cancellationToken);
    }

    public async Task<IPrincipalIdentity?> ResolvePrincipalAsync(
        IIdentitySubject subject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var provider = GetProvider(subject.Scheme);
        
        return await provider.ResolveSubjectAsync(subject.SubjectId, cancellationToken) switch
        {
            IPrincipalIdentity principal => principal,
            IIdentitySubject resolvedSubject => await provider.AuthenticateAsync(new Dictionary<string, string> { ["subjectId"] = resolvedSubject.SubjectId }, cancellationToken),
            _ => null
        };
    }

    public Task<IIdentityLifecycle> GetLifecycleAsync(IIdentitySubject subject, CancellationToken cancellationToken = default)
    {
        return _lifecycleService.GetLifecycleAsync(subject, cancellationToken);
    }

    public Task TransitionAsync(IIdentitySubject subject, IdentityState newState, string? reason = null, CancellationToken cancellationToken = default)
    {
        return _lifecycleService.TransitionAsync(subject, newState, reason, cancellationToken);
    }

    private IIdentityProvider GetProvider(IdentityScheme scheme)
    {
        if (_providers.TryGetValue(scheme, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"No identity provider is registered for scheme '{scheme}'.");
    }
}
