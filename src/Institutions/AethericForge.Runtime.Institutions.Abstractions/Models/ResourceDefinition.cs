namespace AethericForge.Runtime.Institutions.Abstractions.Models;

public sealed record ResourceDefinition
{
    public ResourceDefinition(string name, string type, string description)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public string Name { get; init; }
    public string Type { get; init; }
    public string Description { get; init; }
}