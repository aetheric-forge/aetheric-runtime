namespace AethericForge.Runtime.Institutions.Abstractions.Primitives;

public interface IInstitutionManifest
{
    string Name { get; }

    Version Version { get; }

    string Description { get; }
}
