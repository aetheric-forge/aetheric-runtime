using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Abstractions.Interfaces.Institutions;

public interface IOrganizationContext
{
    /// <summary>
    /// The Institution this Organization is embedded within. Named "Owner" rather than "Parent" since an
    /// Organization does not sit in the Institution hierarchy that capability resolution walks.
    /// </summary>
    IInstitution Owner { get; }

    IInstitutionTemplate Template { get; }

    IServiceProvider Services { get; }
}
