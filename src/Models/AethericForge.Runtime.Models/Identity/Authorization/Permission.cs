using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;
using AethericForge.Runtime.Models.Identity.Directory;

namespace AethericForge.Runtime.Models.Identity.Authorization;

public sealed record Permission : IPermission
{
    public Permission(string scope, string action, string? resource = null)
    {
        Scope = DirectoryValue.NormalizeRequired(scope, nameof(scope));
        Action = DirectoryValue.NormalizeRequired(action, nameof(action));
        Resource = DirectoryValue.NormalizeOptional(resource);
    }

    public string Scope { get; }
    public string Action { get; }
    public string? Resource { get; }
}
