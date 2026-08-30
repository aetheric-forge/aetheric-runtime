namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;

public interface IExternalGroupReference
{
    string Provider { get; }
    string Realm { get; }
    string GroupId { get; }
}
