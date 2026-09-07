using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Faculty.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Institutions.Faculty;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Models.Institutions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Faculty;

public sealed class FacultyTests
{
    private readonly Mock<IFacultyContext> _contextMock = new();
    private readonly Mock<IDean> _deanMock = new();
    private readonly global::AethericForge.Runtime.Institutions.Faculty.Faculty _faculty;

    public FacultyTests()
    {
        _faculty = new global::AethericForge.Runtime.Institutions.Faculty.Faculty(_contextMock.Object, _deanMock.Object);
    }

    [Fact]
    public void Context_ShouldBeSet()
    {
        Assert.Equal(_contextMock.Object, _faculty.Context);
    }

    [Fact]
    public void Dean_ShouldBeSet()
    {
        Assert.Equal(_deanMock.Object, _faculty.Dean);
    }

    [Fact]
    public void Constructor_WithNullDean_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new global::AethericForge.Runtime.Institutions.Faculty.Faculty(_contextMock.Object, null!));
    }

    [Fact]
    public void Dean_ExposesTitleAndTeam()
    {
        var team = new Mock<ITeam<IFacultyClerk>>();
        var dean = new Services.Faculty.Dean("Principal Architect", team.Object);

        Assert.Equal("Principal Architect", dean.Title);
        Assert.Same(team.Object, dean.Team);
    }

    [Fact]
    public void Dean_WithBlankTitle_ShouldThrow()
    {
        var team = new Mock<ITeam<IFacultyClerk>>();

        Assert.Throws<ArgumentException>(
            () => new Services.Faculty.Dean("  ", team.Object));
    }

    [Fact]
    public void Resolve_ShouldFindInstitutionRegisteredOnFaculty_FromInstitutionNestedUnderIt()
    {
        // Proves the shape Brian's dividing Forge Campus into: Campus -> Faculty -> Institution,
        // with resolution walking up through the Faculty exactly like it already does for a plain
        // Institution (see InstitutionCapabilityTests.Resolve_ShouldFindCapabilityInAncestorScope).
        var campus = CreateTestInstitution("campus");
        var facultyTemplate = InstitutionTemplateBuilder.Create()
            .WithDescriptor("architecture-faculty", new Version(1, 0, 0), "Architecture faculty")
            .Build();
        var facultyServices = new ServiceCollection().BuildServiceProvider();
        var facultyContext = new global::AethericForge.Runtime.Institutions.Faculty.FacultyContext(facultyTemplate, facultyServices, campus);
        var facultyTeam = new Mock<ITeam<IFacultyClerk>>();
        var dean = new Services.Faculty.Dean("Principal Architect", facultyTeam.Object);
        var faculty = new global::AethericForge.Runtime.Institutions.Faculty.Faculty(facultyContext, dean);
        campus.Register<global::AethericForge.Runtime.Institutions.Faculty.Faculty>(faculty);

        var decisions = CreateTestInstitution("decisions", faculty);
        var capability = CreateTestCapability("capability", faculty);
        faculty.Register<ITestCapability>(capability);

        Assert.Same(capability, decisions.Resolve<ITestCapability>());
        Assert.Same(faculty, campus.Resolve<global::AethericForge.Runtime.Institutions.Faculty.Faculty>());
    }

    private static TestInstitution CreateTestInstitution(string name, IInstitution? parent = null) =>
        new(CreateContext(name, parent));

    private static TestCapability CreateTestCapability(string name, IInstitution parent) =>
        new(CreateContext(name, parent));

    private static InstitutionContext CreateContext(string name, IInstitution? parent)
    {
        var template = InstitutionTemplateBuilder.Create()
            .WithDescriptor(name, new Version(1, 0, 0), $"{name} institution")
            .Build();
        var services = new ServiceCollection().BuildServiceProvider();

        return new InstitutionContext(template, services, parent);
    }

    private interface ITestCapability : IInstitution;

    private sealed class TestInstitution(IInstitutionContext context) : InstitutionBase(context);

    private sealed class TestCapability(IInstitutionContext context)
        : InstitutionBase(context), ITestCapability;
}
