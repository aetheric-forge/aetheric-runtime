using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Trust;

public interface ITrustRelationship
{
    string RelationshipType { get; }
    IIdentitySubject Trustor { get; }
    IIdentitySubject Trustee { get; }
    DateTimeOffset EstablishedAtUtc { get; }
    DateTimeOffset? ExpiresAtUtc { get; }
}