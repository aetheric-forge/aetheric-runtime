namespace AethericForge.Runtime.Institutions.Abstractions.Models;

public sealed record OrganizationDefinition
{
    public OrganizationDefinition(string name, string description)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public string Name { get; init; }
    public string Description { get; init; }
}