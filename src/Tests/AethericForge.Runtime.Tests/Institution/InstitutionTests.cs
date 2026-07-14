using AethericForge.Runtime.Institutions.Abstractions.Models;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using Xunit;

namespace AethericForge.Runtime.Tests.Institution;

public abstract class InstitutionTests<TInstitution> where TInstitution : IInstitution
{
    protected abstract TInstitution CreateInstitution(IInstitutionContext context);

    protected virtual IInstitutionContext CreateContext() => new TestInstitutionContext();

    [Fact]
    public void Context_ShouldBeSet()
    {
        var context = CreateContext();
        var institution = CreateInstitution(context);
        Assert.Equal(context, institution.Context);
    }
}

public class InstitutionBuilderTests
{
    [Fact]
    public void InstitutionBuilder_ShouldRequireDescriptor()
    {
        var builder = new InstitutionBuilder();
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void InstitutionBuilder_ShouldBuildWithDescriptor()
    {
        var descriptor = new InstitutionDescriptor("test", new Version("1.0.0"), "description");
        var builder = new InstitutionBuilder();
        
        builder.SetDescriptor(descriptor);
        var template = builder.Build();

        Assert.Equal(descriptor, template.Descriptor);
    }

    [Fact]
    public void InstitutionTemplateBuilder_ShouldBuildCorrectly()
    {
        var template = InstitutionTemplateBuilder.Create()
            .WithDescriptor("test", new Version("1.2.3"), "desc")
            .AddDomain(new DomainDefinition("domain1", "domain1-desc"))
            .AddOrganization(new OrganizationDefinition("org1", "org1-desc"))
            .AddRole(new RoleDefinition("role1", "role1-desc"))
            .AddCapability(new CapabilityDefinition("cap1", "cap1-desc"))
            .AddResource(new ResourceDefinition("res1", "type1", "res1-desc"))
            .AddWorkflow(new WorkflowDefinition("wf1", "wf1-desc"))
            .AddPolicy(new PolicyDefinition("pol1", "pol1-desc"))
            .WithInitialState(new InitialStateDefinition())
            .Build();

        Assert.Equal("test", template.Descriptor.Name);
        Assert.Equal(new Version("1.2.3"), template.Descriptor.Version);
        Assert.Single(template.Domains);
        Assert.Single(template.Organizations);
        Assert.Single(template.Roles);
        Assert.Single(template.Capabilities);
        Assert.Single(template.Resources);
        Assert.Single(template.Workflows);
        Assert.Single(template.Policies);
        Assert.NotNull(template.InitialState);
    }

    [Fact]
    public void TestInstitutionContext_ShouldProvideTemplateAndServices()
    {
        var context = new TestInstitutionContext();
        
        Assert.NotNull(context.Template);
        Assert.NotNull(context.Services);
        Assert.Equal("test-institution", context.Template.Descriptor.Name);
    }
}
