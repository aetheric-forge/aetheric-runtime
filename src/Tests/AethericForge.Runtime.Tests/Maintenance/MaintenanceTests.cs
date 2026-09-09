using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Services;
using AethericForge.Runtime.Institutions.Maintenance;
using Moq;

namespace AethericForge.Runtime.Tests.Maintenance;

public sealed class MaintenanceTests
{
    private readonly Mock<IMaintenanceContext> _contextMock = new();
    private readonly Mock<ICaretaker> _caretakerMock = new();
    private readonly Institutions.Maintenance.Maintenance _maintenance;

    public MaintenanceTests()
    {
        _maintenance = new Institutions.Maintenance.Maintenance(_contextMock.Object, _caretakerMock.Object);
    }

    [Fact]
    public void Context_ShouldBeSet()
    {
        Assert.Equal(_contextMock.Object, _maintenance.Context);
    }

    [Fact]
    public void Caretaker_ShouldBeSet()
    {
        Assert.Equal(_caretakerMock.Object, _maintenance.Caretaker);
    }

    [Fact]
    public void Constructor_WithNullCaretaker_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new Institutions.Maintenance.Maintenance(_contextMock.Object, null!));
    }
}
