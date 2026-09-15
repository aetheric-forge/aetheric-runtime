using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Models.Institutions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AethericForge.Runtime.Tests.Institution;

public sealed class OrganizationTests
{
    [Fact]
    public void RegisterOrganization_ShouldResolveById()
    {
        var owner = CreateInstitution("owner");
        var organization = CreateOrganization("first", owner);

        owner.RegisterOrganization("first", organization);

        Assert.Same(organization, owner.ResolveOrganization<TestOrganization>("first"));
    }

    [Fact]
    public void RegisterOrganization_AllowsSeveralOfTheSameShapeUnderDifferentIds()
    {
        var owner = CreateInstitution("owner");
        var first = CreateOrganization("first", owner);
        var second = CreateOrganization("second", owner);

        owner.RegisterOrganization("first", first);
        owner.RegisterOrganization("second", second);

        Assert.Same(first, owner.ResolveOrganization<TestOrganization>("first"));
        Assert.Same(second, owner.ResolveOrganization<TestOrganization>("second"));
    }

    [Fact]
    public void RegisterOrganization_WithNullOrganization_ShouldThrow()
    {
        var owner = CreateInstitution("owner");

        Assert.Throws<ArgumentNullException>(
            () => owner.RegisterOrganization("first", null!));
    }

    [Fact]
    public void RegisterOrganization_WithOrganizationFromAnotherOwner_ShouldThrow()
    {
        var owner = CreateInstitution("owner");
        var otherOwner = CreateInstitution("other-owner");
        var organization = CreateOrganization("first", otherOwner);

        Assert.Throws<ArgumentException>(
            () => owner.RegisterOrganization("first", organization));
    }

    [Fact]
    public void RegisterOrganization_WithDuplicateId_ShouldThrow()
    {
        var owner = CreateInstitution("owner");
        var first = CreateOrganization("first", owner);
        var second = CreateOrganization("second", owner);
        owner.RegisterOrganization("first", first);

        Assert.Throws<InvalidOperationException>(
            () => owner.RegisterOrganization("first", second));
    }

    [Fact]
    public void ResolveOrganization_ShouldNotWalkToAncestorScope()
    {
        var root = CreateInstitution("root");
        var descendant = CreateInstitution("descendant", root);
        var organization = CreateOrganization("first", root);
        root.RegisterOrganization("first", organization);

        Assert.False(descendant.TryResolveOrganization<TestOrganization>("first", out _));
    }

    private static TestInstitution CreateInstitution(string name, IInstitution? parent = null)
    {
        return new TestInstitution(CreateContext(name, parent));
    }

    private static TestOrganization CreateOrganization(string name, IInstitution owner)
    {
        var template = InstitutionTemplateBuilder.Create()
            .WithDescriptor(name, new Version(1, 0, 0), $"{name} organization")
            .Build();
        var services = new ServiceCollection().BuildServiceProvider();

        return new TestOrganization(new OrganizationContext(template, services, owner));
    }

    private static InstitutionContext CreateContext(string name, IInstitution? parent)
    {
        var template = InstitutionTemplateBuilder.Create()
            .WithDescriptor(name, new Version(1, 0, 0), $"{name} institution")
            .Build();
        var services = new ServiceCollection().BuildServiceProvider();

        return new InstitutionContext(template, services, parent);
    }

    private sealed class TestInstitution(IInstitutionContext context)
        : InstitutionBase(context);

    private sealed class TestOrganization(IOrganizationContext context)
        : OrganizationBase(context);
}
