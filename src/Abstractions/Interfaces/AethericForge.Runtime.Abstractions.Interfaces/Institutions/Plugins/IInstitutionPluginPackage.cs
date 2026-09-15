namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions.Plugins;

/// <summary>
/// The entry point a plugin assembly exposes to the host loader. One package may produce several
/// Institution factories.
/// </summary>
public interface IInstitutionPluginPackage
{
    IReadOnlyCollection<IInstitutionFactory> GetFactories();

    /// <summary>
    /// Organization factories this package exposes, if any. Defaults to none so existing packages built
    /// against the Institution-only shape of this interface keep compiling unchanged.
    /// </summary>
    IReadOnlyCollection<IOrganizationFactory> GetOrganizationFactories() => [];
}
