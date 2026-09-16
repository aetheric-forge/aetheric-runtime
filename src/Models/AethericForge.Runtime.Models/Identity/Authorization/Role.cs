using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;
using AethericForge.Runtime.Models.Identity.Directory;

namespace AethericForge.Runtime.Models.Identity.Authorization;

public sealed record Role : IRole
{
    public Role(string name, IReadOnlyCollection<IPermission>? permissions = null)
    {
        Name = DirectoryValue.NormalizeRequired(name, nameof(name));
        Permissions = permissions ?? [];
    }

    public string Name { get; }
    public IReadOnlyCollection<IPermission> Permissions { get; }
}
