using AethericForge.Runtime.Institutions.Abstractions.Models;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Institutions.Abstractions.Builders;

public sealed record InstitutionTemplate : IInstitutionTemplate
{
    public InstitutionTemplate(
        InstitutionDescriptor descriptor,
        IEnumerable<DomainDefinition> domains,
        IEnumerable<OrganizationDefinition> organizations,
        IEnumerable<RoleDefinition> roles,
        IEnumerable<CapabilityDefinition> capabilities,
        IEnumerable<ResourceDefinition> resources,
        IEnumerable<WorkflowDefinition> workflows,
        IEnumerable<PolicyDefinition> policies,
        InitialStateDefinition initialState)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Domains = domains?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(domains));
        Organizations = organizations?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(organizations));
        Roles = roles?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(roles));
        Capabilities = capabilities?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(capabilities));
        Resources = resources?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(resources));
        Workflows = workflows?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(workflows));
        Policies = policies?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(policies));
        InitialState = initialState ?? throw new ArgumentNullException(nameof(initialState));
    }

    public InstitutionDescriptor Descriptor { get; init; }
    public IReadOnlyCollection<DomainDefinition> Domains { get; init; }
    public IReadOnlyCollection<OrganizationDefinition> Organizations { get; init; }
    public IReadOnlyCollection<RoleDefinition> Roles { get; init; }
    public IReadOnlyCollection<CapabilityDefinition> Capabilities { get; init; }
    public IReadOnlyCollection<ResourceDefinition> Resources { get; init; }
    public IReadOnlyCollection<WorkflowDefinition> Workflows { get; init; }
    public IReadOnlyCollection<PolicyDefinition> Policies { get; init; }
    public InitialStateDefinition InitialState { get; init; }
}