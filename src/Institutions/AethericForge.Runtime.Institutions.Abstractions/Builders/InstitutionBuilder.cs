using AethericForge.Runtime.Institutions.Abstractions.Models;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Institutions.Abstractions.Builders;

public class InstitutionBuilder : IInstitutionBuilder
{
    private InstitutionDescriptor? _descriptor;
    private readonly List<DomainDefinition> _domains = new();
    private readonly List<OrganizationDefinition> _organizations = new();
    private readonly List<RoleDefinition> _roles = new();
    private readonly List<CapabilityDefinition> _capabilities = new();
    private readonly List<ResourceDefinition> _resources = new();
    private readonly List<WorkflowDefinition> _workflows = new();
    private readonly List<PolicyDefinition> _policies = new();
    private InitialStateDefinition? _initialState;

    public IInstitutionBuilder SetDescriptor(InstitutionDescriptor descriptor)
    {
        _descriptor = descriptor;
        return this;
    }

    public IInstitutionBuilder AddDomain(DomainDefinition domain)
    {
        _domains.Add(domain);
        return this;
    }

    public IInstitutionBuilder AddOrganization(OrganizationDefinition organization)
    {
        _organizations.Add(organization);
        return this;
    }

    public IInstitutionBuilder AddRole(RoleDefinition role)
    {
        _roles.Add(role);
        return this;
    }

    public IInstitutionBuilder AddCapability(CapabilityDefinition capability)
    {
        _capabilities.Add(capability);
        return this;
    }

    public IInstitutionBuilder AddResource(ResourceDefinition resource)
    {
        _resources.Add(resource);
        return this;
    }

    public IInstitutionBuilder AddWorkflow(WorkflowDefinition workflow)
    {
        _workflows.Add(workflow);
        return this;
    }

    public IInstitutionBuilder AddPolicy(PolicyDefinition policy)
    {
        _policies.Add(policy);
        return this;
    }

    public IInstitutionBuilder SetInitialState(InitialStateDefinition initialState)
    {
        _initialState = initialState;
        return this;
    }

    public IInstitutionBuilder AddModule(IInstitutionModule module)
    {
        module.Configure(this);
        return this;
    }

    public IInstitutionTemplate Build()
    {
        if (_descriptor == null)
        {
            throw new InvalidOperationException("InstitutionDescriptor must be set before building.");
        }

        return new InstitutionTemplate(
            _descriptor,
            _domains,
            _organizations,
            _roles,
            _capabilities,
            _resources,
            _workflows,
            _policies,
            _initialState ?? new InitialStateDefinition());
    }
}