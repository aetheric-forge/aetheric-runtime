using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;
using AethericForge.Runtime.Models.Identity.Directory;

namespace AethericForge.Runtime.Models.Identity.Authorization;

public sealed record Group : IGroup
{
    public Group(string name, string path)
    {
        Name = DirectoryValue.NormalizeRequired(name, nameof(name));
        Path = DirectoryValue.NormalizeRequired(path, nameof(path));
    }

    public string Name { get; }
    public string Path { get; }
}
