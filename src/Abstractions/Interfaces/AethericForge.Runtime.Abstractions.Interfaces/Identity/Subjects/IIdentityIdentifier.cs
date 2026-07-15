namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;

using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;

public interface IIdentityIdentifier
{
    string SubjectId { get; }
    IdentityScheme Scheme { get; }
}