namespace AethericForge.Runtime.Abstractions.Interfaces.Identity.Authorization;

public interface IGroup
{
    string Name { get; }

    /// <summary>The provider's full hierarchical path, e.g. <c>/teams/adr-campus-members</c>.</summary>
    string Path { get; }
}
