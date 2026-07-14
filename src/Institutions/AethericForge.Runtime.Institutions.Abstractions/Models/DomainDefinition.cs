namespace AethericForge.Runtime.Institutions.Abstractions.Models;

public sealed record DomainDefinition
{
    public DomainDefinition(
        string name, 
        string description, 
        IEnumerable<CapabilityDefinition>? requiredCapabilities = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        RequiredCapabilities = requiredCapabilities?.ToHashSet() ?? new HashSet<CapabilityDefinition>();
    }

    public string Name { get; init; }
    public string Description { get; init; }
    public IReadOnlySet<CapabilityDefinition> RequiredCapabilities { get; init; }
}