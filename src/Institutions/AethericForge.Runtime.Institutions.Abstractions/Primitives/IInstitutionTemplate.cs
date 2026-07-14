using AethericForge.Runtime.Institutions.Abstractions.Models;

namespace AethericForge.Runtime.Institutions.Abstractions.Primitives;

public interface IInstitutionTemplate
{
    InstitutionDescriptor Descriptor { get; }

    IReadOnlyCollection<DomainDefinition> Domains { get; }

    IReadOnlyCollection<OrganizationDefinition> Organizations { get; }

    IReadOnlyCollection<RoleDefinition> Roles { get; }

    IReadOnlyCollection<CapabilityDefinition> Capabilities { get; }

    IReadOnlyCollection<ResourceDefinition> Resources { get; }

    IReadOnlyCollection<WorkflowDefinition> Workflows { get; }

    IReadOnlyCollection<PolicyDefinition> Policies { get; }

    InitialStateDefinition InitialState { get; }
}