using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Campus;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace AethericForge.Runtime.Tests.Institution;

public class CampusTests : InstitutionTests<Campus>
{
    protected override Campus CreateInstitution(IInstitutionContext context)
    {
        return new Campus((ICampusContext)context);
    }

    protected override ICampusContext CreateContext()
    {
        var template = InstitutionTemplateBuilder.Create()
            .UseModule<CampusModule>()
            .Build();

        var services = new ServiceCollection().BuildServiceProvider();
        return new CampusContext(template, services);
    }

    [Fact]
    public void Campus_ShouldHaveNoParent()
    {
        var context = CreateContext();
        Assert.Null(context.Parent);
    }

    [Fact]
    public void Campus_ShouldHaveCorrectDescriptor()
    {
        var context = CreateContext();
        Assert.Equal("Campus", context.Template.Descriptor.Name);
    }

    [Fact]
    public async Task Campus_LifecycleMethods_ShouldNotThrow()
    {
        var context = CreateContext();
        var campus = CreateInstitution(context);

        await campus.InitializeAsync();
        await campus.StartAsync();
        await campus.StopAsync();
    }
    
    [Fact]
    public void Constructor_WithNullContext_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new Campus(null!));
    }
}
