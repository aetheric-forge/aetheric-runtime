namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;

public interface IRole
{
    string Name { get; }
    IReadOnlyCollection<IPermission> Permissions { get; }
}
