using AethericForge.Runtime.Institutions.Abstractions.Models;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;

namespace AethericForge.Runtime.Tests.Institution;

internal sealed class TestInstitutionTemplate : IInstitutionTemplate
{
    private readonly IInstitutionTemplate _inner;

    public TestInstitutionTemplate()
    {
        _inner = InstitutionTemplateBuilder.Create()
            .WithDescriptor("test-institution", new Version("1.0.0"), "test-institution-description")
            .Build();
    }

    public InstitutionDescriptor Descriptor => _inner.Descriptor;

    public IReadOnlyCollection<DomainDefinition> Domains => _inner.Domains;

    public IReadOnlyCollection<OrganizationDefinition> Organizations => _inner.Organizations;

    public IReadOnlyCollection<RoleDefinition> Roles => _inner.Roles;

    public IReadOnlyCollection<CapabilityDefinition> Capabilities => _inner.Capabilities;

    public IReadOnlyCollection<ResourceDefinition> Resources => _inner.Resources;

    public IReadOnlyCollection<WorkflowDefinition> Workflows => _inner.Workflows;

    public IReadOnlyCollection<PolicyDefinition> Policies => _inner.Policies;

    public InitialStateDefinition InitialState => _inner.InitialState;
}
