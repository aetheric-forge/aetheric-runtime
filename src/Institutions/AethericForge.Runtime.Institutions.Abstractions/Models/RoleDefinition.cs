namespace AethericForge.Runtime.Institutions.Abstractions.Models;

public sealed record RoleDefinition
{
    public RoleDefinition(
        string name, 
        string description, 
        IEnumerable<CapabilityDefinition>? capabilities = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Capabilities = capabilities?.ToHashSet() ?? new HashSet<CapabilityDefinition>();
    }

    public string Name { get; init; }
    public string Description { get; init; }
    public IReadOnlySet<CapabilityDefinition> Capabilities { get; init; }
}