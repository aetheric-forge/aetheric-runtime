using AethericForge.Runtime.Abstractions.Interfaces.Identity.Core;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Services;

public interface IAuthenticationService
{
    Task<IPrincipalIdentity?> AuthenticateAsync(
        IdentityScheme scheme,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken = default);
}
