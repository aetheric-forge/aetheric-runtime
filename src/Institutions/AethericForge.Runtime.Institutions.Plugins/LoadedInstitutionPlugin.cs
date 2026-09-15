using AethericForge.Runtime.Abstractions.Interfaces.Institutions.Plugins;

namespace AethericForge.Runtime.Institutions.Plugins;

/// <summary>
/// One successfully loaded plugin assembly and the Institution factories it exposed.
/// </summary>
public sealed record LoadedInstitutionPlugin(
    string AssemblyPath,
    IReadOnlyCollection<IInstitutionFactory> Factories);
