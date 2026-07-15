using AethericForge.Runtime.Institutions.Abstractions.Models;

namespace AethericForge.Runtime.Institutions.Abstractions.Primitives;

public interface IInstitutionBuilder
{
    IInstitutionBuilder SetDescriptor(InstitutionDescriptor descriptor);
    IInstitutionBuilder AddDomain(DomainDefinition domain);
    IInstitutionBuilder AddOrganization(OrganizationDefinition organization);
    IInstitutionBuilder AddRole(RoleDefinition role);
    IInstitutionBuilder AddCapability(CapabilityDefinition capability);
    IInstitutionBuilder AddResource(ResourceDefinition resource);
    IInstitutionBuilder AddWorkflow(WorkflowDefinition workflow);
    IInstitutionBuilder AddPolicy(PolicyDefinition policy);
    IInstitutionBuilder SetInitialState(InitialStateDefinition initialState);
    
    IInstitutionBuilder AddModule(IInstitutionModule module);

    IInstitutionTemplate Build();
}