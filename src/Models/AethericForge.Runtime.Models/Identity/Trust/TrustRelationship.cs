using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Trust;

namespace AethericForge.Runtime.Models.Identity.Trust;

public sealed record TrustRelationship(
    string RelationshipType,
    IIdentitySubject Trustor,
    IIdentitySubject Trustee,
    DateTimeOffset EstablishedAtUtc,
    DateTimeOffset? ExpiresAtUtc = null) : ITrustRelationship;
