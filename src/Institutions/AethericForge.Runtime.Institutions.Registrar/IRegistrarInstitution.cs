using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Principals;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;

namespace AethericForge.Runtime.Institutions.Registrar;

public interface IRegistrarInstitution : IInstitution
{
    Task<IPrincipalIdentity> RegisterAsync(
        IIdentitySubject subject,
        CancellationToken ct = default);

    Task<IPrincipalIdentity?> IdentifyAsync(
        IIdentitySubject subject,
        CancellationToken ct = default);

    Task<IPrincipalIdentity?> AuthenticateAsync(
        IdentityCredentials credentials,
        CancellationToken ct = default);

    Task<bool> ExistsAsync(
        IIdentitySubject subject,
        CancellationToken ct = default);
}
