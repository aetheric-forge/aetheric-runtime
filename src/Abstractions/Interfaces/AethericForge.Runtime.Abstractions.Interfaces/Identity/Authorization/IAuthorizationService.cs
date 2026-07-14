using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;

public interface IAuthorizationService
{
    Task<bool> AuthorizeAsync(
        IPrincipalIdentity principal,
        IPermission permission,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IPermission>> GetPermissionsAsync(
        IPrincipalIdentity principal,
        CancellationToken cancellationToken = default);
}
