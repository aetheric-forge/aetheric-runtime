namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;

public interface IPermission
{
    string Scope { get; }
    string Action { get; }
    string? Resource { get; }
}
