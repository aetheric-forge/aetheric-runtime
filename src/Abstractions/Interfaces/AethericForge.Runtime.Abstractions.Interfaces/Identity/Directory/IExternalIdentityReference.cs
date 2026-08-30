namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;

public interface IExternalIdentityReference
{
    string Provider { get; }
    string Realm { get; }
    string SubjectId { get; }
}
