using System.Reflection;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AethericForge.Runtime.Institutions.Plugins;

/// <summary>
/// Discovers and loads Institution plugin packages from a directory of assemblies. Each assembly is
/// loaded into its own isolated <see cref="InstitutionPluginLoadContext"/>; a plugin that fails to load,
/// or that doesn't declare an <see cref="InstitutionPluginPackageAttribute"/>, is skipped rather than
/// failing the whole load, so one broken plugin doesn't take every other Institution down with it.
/// </summary>
public sealed class InstitutionPluginLoader(ILogger<InstitutionPluginLoader>? logger = null)
{
    private readonly ILogger<InstitutionPluginLoader> _logger = logger ?? NullLogger<InstitutionPluginLoader>.Instance;

    public IReadOnlyCollection<LoadedInstitutionPlugin> LoadDirectory(string pluginDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        if (!Directory.Exists(pluginDirectory))
        {
            _logger.LogWarning("Plugin directory {PluginDirectory} does not exist; no Institution plugins loaded.", pluginDirectory);
            return [];
        }

        var loaded = new List<LoadedInstitutionPlugin>();

        foreach (var assemblyPath in Directory.EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var plugin = LoadAssembly(assemblyPath);
                if (plugin is not null)
                {
                    loaded.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Institution plugin from {AssemblyPath}; skipping it.", assemblyPath);
            }
        }

        return loaded;
    }

    private LoadedInstitutionPlugin? LoadAssembly(string assemblyPath)
    {
        var context = new InstitutionPluginLoadContext(assemblyPath);
        var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
        var assembly = context.LoadFromAssemblyName(assemblyName);

        var packageAttribute = assembly.GetCustomAttribute<InstitutionPluginPackageAttribute>();
        if (packageAttribute is null)
        {
            _logger.LogWarning(
                "{AssemblyPath} does not declare [assembly: InstitutionPluginPackage(...)]; skipping it.",
                assemblyPath);
            return null;
        }

        if (Activator.CreateInstance(packageAttribute.PackageType) is not IInstitutionPluginPackage package)
        {
            _logger.LogError(
                "{AssemblyPath} declares {PackageType} as its plugin package, but it does not implement " +
                "IInstitutionPluginPackage or could not be constructed; skipping it.",
                assemblyPath,
                packageAttribute.PackageType);
            return null;
        }

        var factories = package.GetFactories();

        _logger.LogInformation(
            "Loaded Institution plugin {AssemblyPath} exposing {FactoryCount} factory(ies).",
            assemblyPath,
            factories.Count);

        return new LoadedInstitutionPlugin(assemblyPath, factories);
    }
}
