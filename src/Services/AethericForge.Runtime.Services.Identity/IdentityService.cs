using AethericForge.Runtime.Abstractions.Interfaces.Identity.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;

namespace AethericForge.Runtime.Services.Identity;

public sealed class IdentityService : IIdentityService
{
    private readonly IReadOnlyDictionary<IdentityScheme, IIdentityProvider> _providers;

    public IdentityService(IEnumerable<IIdentityProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.ToDictionary(p => p.Scheme);
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
        
        // This is a bit simplified, usually you'd want to verify the subject still exists or refresh claims
        return await provider.ResolveSubjectAsync(subject.SubjectId, cancellationToken) switch
        {
            IPrincipalIdentity principal => principal,
            IIdentitySubject resolvedSubject => await provider.AuthenticateAsync(new Dictionary<string, string> { ["subjectId"] = resolvedSubject.SubjectId }, cancellationToken),
            _ => null
        };
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
