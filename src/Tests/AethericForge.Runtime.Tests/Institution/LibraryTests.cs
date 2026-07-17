using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Library;
using AethericForge.Runtime.Services.Library;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Institution;

public class LibraryTests : InstitutionTests<Library>
{
    private readonly Mock<IKnowledgeService> _knowledgeServiceMock = new();

    protected override Library CreateInstitution(IInstitutionContext context)
    {
        var librarian = new Librarian(
            _knowledgeServiceMock.Object, 
            Mock.Of<ITeam<ILibraryClerk>>());

        return new Library(
            (ILibraryContext)context, 
            librarian);
    }

    protected override ILibraryContext CreateContext()
    {
        var template = InstitutionTemplateBuilder.Create()
            .UseModule<LibraryModule>()
            .Build();

        var services = new ServiceCollection()
            .AddSingleton(_knowledgeServiceMock.Object)
            .BuildServiceProvider();

        return new LibraryContext(template, services);
    }

    [Fact]
    public void Library_ShouldHaveCorrectDescriptor()
    {
        var context = CreateContext();
        Assert.Equal("Library", context.Template.Descriptor.Name);
    }

    [Fact]
    public async Task GetArtifactAsync_ShouldDelegateToKnowledgeService()
    {
        var context = CreateContext();
        var library = CreateInstitution(context);
        var reference = Mock.Of<AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives.IKnowledgeReference>();

        await library.Librarian.GetArtifactAsync(reference);

        _knowledgeServiceMock.Verify(s => s.GetArtifactAsync(reference, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishArtifactAsync_ShouldDelegateToKnowledgeService()
    {
        var context = CreateContext();
        var library = CreateInstitution(context);
        var descriptor = Mock.Of<AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives.IKnowledgeDescriptor>();
        var representations = Enumerable.Empty<AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations.IKnowledgeRepresentation>();

        await library.Librarian.PublishArtifactAsync(descriptor, representations);

        _knowledgeServiceMock.Verify(s => s.PublishArtifactAsync(
            descriptor,
            representations,
            It.IsAny<IEnumerable<AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives.IKnowledgeReference>>(),
            It.IsAny<AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities.IKnowledgeAuthority>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
