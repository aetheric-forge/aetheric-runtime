using AethericForge.Runtime.Abstractions.Interfaces.Faculty.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Abstractions.Primitives;
using AethericForge.Runtime.Institutions.Campus;
using AethericForge.Runtime.Institutions.Faculty;
using AethericForge.Runtime.Models.Institutions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AethericForge.Runtime.Tests.Institution;

public class CampusCompositionExtensionsTests
{
    // Fakes only take a context, isolating the helper's own logic (template derivation,
    // ActivatorUtilities construction, Register call) from any real institution's DI requirements.
    private interface IFakeInstitution : IInstitution;

    private sealed class FakeInstitution(IInstitutionContext context)
        : InstitutionBase(context), IFakeInstitution;

    private sealed class FakeInstitutionContext(
        IInstitutionTemplate template, IServiceProvider services, IInstitution? parent = null)
        : InstitutionContext(template, services, parent);

    private interface IFakeFaculty : IFaculty;

    private sealed class FakeFaculty(IFacultyContext context, IDean dean)
        : InstitutionBase(context), IFakeFaculty
    {
        public IDean Dean { get; } = dean;
    }

    private static (Campus campus, InstitutionTemplate template, IServiceProvider services) CreateCampus()
    {
        var template = InstitutionTemplateBuilder.Create()
            .UseModule<CampusModule>()
            .Build();
        var services = new ServiceCollection().BuildServiceProvider();
        var campus = new Campus(new CampusContext((InstitutionTemplate)template, services));
        return (campus, (InstitutionTemplate)template, services);
    }

    [Fact]
    public void RegisterInstitution_ConstructsAndRegistersResolvableInstitution()
    {
        var (campus, template, services) = CreateCampus();

        var registered = campus.RegisterInstitution<IFakeInstitution, FakeInstitution, FakeInstitutionContext>(
            template, services, "Fake",
            (t, sp, parent) => new FakeInstitutionContext(t, sp, parent));

        Assert.NotNull(registered);
        Assert.Same(registered, campus.Resolve<IFakeInstitution>());
    }

    [Fact]
    public void RegisterInstitution_DerivesPerInstitutionDescriptorFromCampusTemplate()
    {
        var (campus, template, services) = CreateCampus();

        IInstitutionTemplate? capturedTemplate = null;
        campus.RegisterInstitution<IFakeInstitution, FakeInstitution, FakeInstitutionContext>(
            template, services, "Fake",
            (t, sp, parent) =>
            {
                capturedTemplate = t;
                return new FakeInstitutionContext(t, sp, parent);
            });

        Assert.NotNull(capturedTemplate);
        Assert.Equal("Fake", capturedTemplate!.Descriptor.Name);
        Assert.Equal(template.Descriptor.Version, capturedTemplate.Descriptor.Version);
    }

    [Fact]
    public void RegisterFaculty_ConstructsAndRegistersResolvableFaculty()
    {
        var (campus, template, services) = CreateCampus();

        var faculty = campus.RegisterFaculty<IFakeFaculty>(
            template, services, "Engineering", "Dean of Engineering",
            (context, dean) => new FakeFaculty(context, dean));

        Assert.NotNull(faculty);
        Assert.Equal("Dean of Engineering", faculty.Dean.Title);
        Assert.Same(faculty, campus.Resolve<IFakeFaculty>());
    }
}
