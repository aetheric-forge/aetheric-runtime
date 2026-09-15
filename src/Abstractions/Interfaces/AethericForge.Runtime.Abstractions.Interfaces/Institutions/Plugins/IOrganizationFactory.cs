using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions.Plugins;

/// <summary>
/// Builds and mounts one Organization beneath an owning Institution. Unlike <see cref="IInstitutionFactory"/>,
/// there is no <c>ContractType</c> - Organizations are registered by <see cref="OrganizationId"/> via
/// <see cref="IInstitution.RegisterOrganization"/>, since several Organizations of the same shape can
/// coexist under one owner.
/// </summary>
public interface IOrganizationFactory
{
    /// <summary>
    /// The id this factory's Organization registers under. This is the key passed to
    /// <see cref="IInstitution.RegisterOrganization"/>, not a contract type.
    /// </summary>
    string OrganizationId { get; }

    IInstitutionManifest Manifest { get; }

    IInstitutionTemplate Template { get; }

    /// <summary>
    /// Constructs the Organization as embedded within <paramref name="owner"/>. The returned instance's
    /// <c>Context.Owner</c> must be <paramref name="owner"/>, since that is what
    /// <see cref="IInstitution.RegisterOrganization"/> requires of anything registered into its scope.
    /// </summary>
    IOrganization Create(IInstitution owner, IServiceProvider services);
}
