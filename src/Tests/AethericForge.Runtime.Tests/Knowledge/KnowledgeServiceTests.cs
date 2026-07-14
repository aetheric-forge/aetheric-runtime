using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Knowledge.Artifacts;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.References;
using AethericForge.Runtime.Services.Knowledge;
using Xunit;
using Moq;

namespace AethericForge.Runtime.Tests.Knowledge;

public class KnowledgeServiceTests
{
    [Fact]
    public async Task GetArtifactAsync_ReturnsArtifact_FromCorrectProvider()
    {
        // Arrange
        var reference = new KnowledgeReference("Primary", "Artifact", "Test", "1.0");
        var artifact = new Mock<IKnowledgeArtifact>().Object;
        
        var provider = new Mock<IKnowledgeProvider>();
        provider.Setup(p => p.Scheme).Returns("Primary");
        provider.Setup(p => p.GetArtifactAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(artifact);

        var service = new KnowledgeService(new[] { provider.Object });

        // Act
        var result = await service.GetArtifactAsync(reference);

        // Assert
        Assert.Same(artifact, result);
    }

    [Fact]
    public async Task PublishArtifactAsync_UsesFirstAvailableProvider()
    {
        // Arrange
        var descriptor = new KnowledgeDescriptor("Title");
        var representations = new List<IKnowledgeRepresentation>();
        var artifact = new Mock<IKnowledgeArtifact>().Object;

        var provider = new Mock<IKnowledgeProvider>();
        provider.Setup(p => p.Scheme).Returns("Primary");
        provider.Setup(p => p.StoreArtifactAsync(descriptor, representations, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(artifact);

        var service = new KnowledgeService(new[] { provider.Object });

        // Act
        var result = await service.PublishArtifactAsync(descriptor, representations);

        // Assert
        Assert.Same(artifact, result);
    }

    [Fact]
    public async Task ResolveReferenceAsync_HandlesAuthoritativeReference()
    {
        // Arrange
        var identity = new Mock<IIdentitySubject>();
        identity.Setup(i => i.SubjectId).Returns("Author1");
        var authority = new KnowledgeAuthority(identity.Object, "Global");
        
        var authRef = new AuthoritativeReference("Primary", "Artifact", "Test", "latest", authority, "Current");
        var artifact = new Mock<IKnowledgeArtifact>().Object;

        var provider = new Mock<IKnowledgeProvider>();
        provider.Setup(p => p.Scheme).Returns("Primary");
        provider.Setup(p => p.GetArtifactAsync(authRef, It.IsAny<CancellationToken>()))
            .ReturnsAsync(artifact);

        var service = new KnowledgeService(new[] { provider.Object });

        // Act
        var result = await service.ResolveReferenceAsync(authRef);

        // Assert
        Assert.Same(artifact, result);
    }
}
