using AethericForge.Runtime.Abstractions.Interfaces.Identity.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;

public interface IAuthenticationService
{
    Task<IPrincipalIdentity?> AuthenticateAsync(
        IdentityScheme scheme,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken = default);
}
