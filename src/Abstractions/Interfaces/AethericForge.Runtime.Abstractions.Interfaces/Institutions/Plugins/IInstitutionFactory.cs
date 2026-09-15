using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions.Plugins;

/// <summary>
/// Builds and mounts one Institution beneath a parent scope. A plugin package exposes one of these per
/// Institution it can produce; the host loader constructs the Institution and registers it into the
/// parent under <see cref="ContractType"/>.
/// </summary>
public interface IInstitutionFactory
{
    /// <summary>
    /// The specialized Institution contract (e.g. <c>typeof(IDecisions)</c>) this factory registers under.
    /// This is the key passed to <see cref="IInstitution.Register{TInstitution}"/>.
    /// </summary>
    Type ContractType { get; }

    IInstitutionManifest Manifest { get; }

    IInstitutionTemplate Template { get; }

    /// <summary>
    /// Constructs the Institution as a child of <paramref name="parent"/>. The returned instance's
    /// <c>Context.Parent</c> must be <paramref name="parent"/>, since that is what
    /// <see cref="IInstitution.Register{TInstitution}"/> requires of anything registered into its scope.
    /// </summary>
    IInstitution Create(IInstitution parent, IServiceProvider services);
}
