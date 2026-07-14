using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Institutions.Abstractions.Models;

public sealed record InstitutionDescriptor : IInstitutionManifest
{
    public InstitutionDescriptor(string name, Version version, string description)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public string Name { get; init; }
    public Version Version { get; init; }
    public string Description { get; init; }
}