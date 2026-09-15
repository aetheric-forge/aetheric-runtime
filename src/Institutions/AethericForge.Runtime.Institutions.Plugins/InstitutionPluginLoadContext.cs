using System.Reflection;
using System.Runtime.Loader;

namespace AethericForge.Runtime.Institutions.Plugins;

/// <summary>
/// An isolated, collectible load context for one plugin assembly. Dependencies private to the plugin
/// (its own libraries, a different Mongo driver version, etc.) resolve from the plugin's own output
/// directory and never collide with the host or with other plugins. Assemblies that carry the shared
/// Institution contracts are deliberately left unresolved here so the default probing falls through to
/// <see cref="AssemblyLoadContext.Default"/>, which is what lets an <c>IInstitution</c> instance built
/// inside the plugin be registered and cast by the host without a type-identity mismatch.
/// </summary>
internal sealed class InstitutionPluginLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AethericForge.Runtime.Abstractions.Interfaces",
        "AethericForge.Runtime.Institutions.Abstractions",
        "AethericForge.Runtime.Models",
    };

    private readonly AssemblyDependencyResolver _resolver;

    public InstitutionPluginLoadContext(string pluginAssemblyPath)
        : base(name: Path.GetFileNameWithoutExtension(pluginAssemblyPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && SharedAssemblyNames.Contains(assemblyName.Name))
        {
            return null;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is not null ? LoadUnmanagedDllFromPath(libraryPath) : nint.Zero;
    }
}
