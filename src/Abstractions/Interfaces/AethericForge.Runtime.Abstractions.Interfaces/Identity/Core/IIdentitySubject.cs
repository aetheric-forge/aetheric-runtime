namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Core;

public interface IIdentitySubject
{
    string SubjectId { get; }
    IdentityScheme Scheme { get; }
    string? DisplayName { get; }
    IdentityState State { get; }
    IReadOnlyCollection<IIdentityClaim> Claims { get; }
}
