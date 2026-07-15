using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Models.Institutions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AethericForge.Runtime.Tests.Institution;

public sealed class InstitutionCapabilityTests
{
    [Fact]
    public void Register_ShouldResolveLocalCapability()
    {
        var parent = CreateInstitution("parent");
        var child = CreateCapability("child", parent);

        parent.Register<ITestCapability>(child);

        Assert.Same(child, parent.Resolve<ITestCapability>());
    }

    [Fact]
    public void Register_WithNullInstitution_ShouldThrow()
    {
        var parent = CreateInstitution("parent");

        Assert.Throws<ArgumentNullException>(
            () => parent.Register<ITestCapability>(null!));
    }

    [Fact]
    public void Register_WithInstitutionFromAnotherScope_ShouldThrow()
    {
        var parent = CreateInstitution("parent");
        var otherParent = CreateInstitution("other-parent");
        var child = CreateCapability("child", otherParent);

        Assert.Throws<ArgumentException>(
            () => parent.Register<ITestCapability>(child));
    }

    [Fact]
    public void Register_WithDuplicateCapability_ShouldThrow()
    {
        var parent = CreateInstitution("parent");
        var first = CreateCapability("first", parent);
        var second = CreateCapability("second", parent);
        parent.Register<ITestCapability>(first);

        Assert.Throws<InvalidOperationException>(
            () => parent.Register<ITestCapability>(second));
    }

    [Fact]
    public void Resolve_ShouldFindCapabilityInAncestorScope()
    {
        var root = CreateInstitution("root");
        var parent = CreateInstitution("parent", root);
        var descendant = CreateInstitution("descendant", parent);
        var capability = CreateCapability("capability", root);
        root.Register<ITestCapability>(capability);

        Assert.Same(capability, descendant.Resolve<ITestCapability>());
    }

    private static TestInstitution CreateInstitution(
        string name,
        IInstitution? parent = null)
    {
        return new TestInstitution(CreateContext(name, parent));
    }

    private static TestCapability CreateCapability(
        string name,
        IInstitution parent)
    {
        return new TestCapability(CreateContext(name, parent));
    }

    private static InstitutionContext CreateContext(
        string name,
        IInstitution? parent)
    {
        var template = InstitutionTemplateBuilder.Create()
            .WithDescriptor(name, new Version(1, 0, 0), $"{name} institution")
            .Build();
        var services = new ServiceCollection().BuildServiceProvider();

        return new InstitutionContext(template, services, parent);
    }

    private interface ITestCapability : IInstitution;

    private sealed class TestInstitution(IInstitutionContext context)
        : InstitutionBase(context);

    private sealed class TestCapability(IInstitutionContext context)
        : InstitutionBase(context), ITestCapability;
}
