namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions.Plugins;

/// <summary>
/// Declares a plugin assembly's <see cref="IInstitutionPluginPackage"/> entry type, so the host loader can
/// find it deterministically instead of scanning every public type for a match.
/// </summary>
/// <example>
/// <c>[assembly: InstitutionPluginPackage(typeof(DecisionsPluginPackage))]</c>
/// </example>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class InstitutionPluginPackageAttribute(Type packageType) : Attribute
{
    public Type PackageType { get; } = packageType ?? throw new ArgumentNullException(nameof(packageType));
}
