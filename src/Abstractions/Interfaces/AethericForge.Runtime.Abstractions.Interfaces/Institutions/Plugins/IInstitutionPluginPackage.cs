namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions.Plugins;

/// <summary>
/// The entry point a plugin assembly exposes to the host loader. One package may produce several
/// Institution factories.
/// </summary>
public interface IInstitutionPluginPackage
{
    IReadOnlyCollection<IInstitutionFactory> GetFactories();
}
