using AethericForge.Runtime.Institutions.Abstractions.Models;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;

namespace AethericForge.Runtime.Institutions.Abstractions.Builders;

public sealed class InstitutionTemplateBuilder
{
    private readonly IInstitutionBuilder _builder;

    private InstitutionTemplateBuilder(IInstitutionBuilder builder)
    {
        _builder = builder;
    }

    public static InstitutionTemplateBuilder Create()
    {
        return new InstitutionTemplateBuilder(new InstitutionBuilder());
    }

    public InstitutionTemplateBuilder WithDescriptor(InstitutionDescriptor descriptor)
    {
        _builder.SetDescriptor(descriptor);
        return this;
    }

    public InstitutionTemplateBuilder WithDescriptor(string name, Version version, string description)
    {
        _builder.SetDescriptor(new InstitutionDescriptor(name, version, description));
        return this;
    }

    public InstitutionTemplateBuilder AddDomain(DomainDefinition domain)
    {
        _builder.AddDomain(domain);
        return this;
    }

    public InstitutionTemplateBuilder AddOrganization(OrganizationDefinition organization)
    {
        _builder.AddOrganization(organization);
        return this;
    }

    public InstitutionTemplateBuilder AddRole(RoleDefinition role)
    {
        _builder.AddRole(role);
        return this;
    }

    public InstitutionTemplateBuilder AddCapability(CapabilityDefinition capability)
    {
        _builder.AddCapability(capability);
        return this;
    }

    public InstitutionTemplateBuilder AddResource(ResourceDefinition resource)
    {
        _builder.AddResource(resource);
        return this;
    }

    public InstitutionTemplateBuilder AddWorkflow(WorkflowDefinition workflow)
    {
        _builder.AddWorkflow(workflow);
        return this;
    }

    public InstitutionTemplateBuilder AddPolicy(PolicyDefinition policy)
    {
        _builder.AddPolicy(policy);
        return this;
    }

    public InstitutionTemplateBuilder WithInitialState(InitialStateDefinition initialState)
    {
        _builder.SetInitialState(initialState);
        return this;
    }

    public InstitutionTemplateBuilder UseModule(IInstitutionModule module)
    {
        _builder.AddModule(module);
        return this;
    }

    public InstitutionTemplateBuilder UseModule<TModule>() where TModule : IInstitutionModule, new()
    {
        _builder.AddModule(new TModule());
        return this;
    }

    public IInstitutionTemplate Build()
    {
        return _builder.Build();
    }
}