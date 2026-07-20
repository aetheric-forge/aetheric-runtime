using AethericForge.Runtime.Abstractions.Interfaces.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Models.Knowledge.Artifacts;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.References;
using AethericForge.Runtime.Providers.Knowledge.InMemory;
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
        var provider = new InMemoryKnowledgeProvider("Primary");
        var descriptor = new KnowledgeDescriptor("Title");
        var identity = new Mock<IIdentitySubject>();
        var authority = new KnowledgeAuthority(identity.Object, "Global");
        var artifact = await provider.StoreArtifactAsync(descriptor, new List<IKnowledgeRepresentation>(), null, authority);
        var service = new KnowledgeService(new[] { provider }, Mock.Of<ITeam<ICuratorClerk>>());
        var result = await service.GetArtifactAsync(artifact.Reference);

        // Assert
        Assert.Equal(artifact.Reference, result.Reference);
    }

    [Fact]
    public async Task PublishArtifactAsync_UsesFirstAvailableProvider()
    {
        // Arrange
        var descriptor = new KnowledgeDescriptor("Title");
        var representations = new List<IKnowledgeRepresentation>();
        var identity = new Mock<IIdentitySubject>();
        var authority = new KnowledgeAuthority(identity.Object, "Global");

        var provider = new InMemoryKnowledgeProvider("test-scheme");
        var service = new KnowledgeService(new[] { provider }, Mock.Of<ITeam<ICuratorClerk>>());

        // Act
        var result = await service.PublishArtifactAsync(descriptor, representations, authority: authority);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-scheme", result.Reference.Scheme);
    }

    [Fact]
    public async Task FindArtifactsAsync_AggregatesProviders()
    {
        var authority = Mock.Of<IKnowledgeAuthority>();
        var firstArtifact = Mock.Of<IKnowledgeArtifact>();
        var secondArtifact = Mock.Of<IKnowledgeArtifact>();
        var firstProvider = new Mock<IKnowledgeProvider>();
        var secondProvider = new Mock<IKnowledgeProvider>();

        firstProvider.SetupGet(provider => provider.Scheme).Returns("first");
        secondProvider.SetupGet(provider => provider.Scheme).Returns("second");
        firstProvider
            .Setup(provider => provider.FindArtifactsAsync(
                authority,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { firstArtifact });
        secondProvider
            .Setup(provider => provider.FindArtifactsAsync(
                authority,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { secondArtifact });

        var service = new KnowledgeService(
            [firstProvider.Object, secondProvider.Object],
            Mock.Of<ITeam<ICuratorClerk>>());

        var results = await service.FindArtifactsAsync(authority);

        Assert.Contains(firstArtifact, results);
        Assert.Contains(secondArtifact, results);
    }

    [Fact]
    public async Task ResolveReferenceAsync_ResolvesAuthoritativeReference()
    {
        // Arrange
        var identity = new Mock<IIdentitySubject>();
        identity.Setup(i => i.SubjectId).Returns("Author1");
        var authority = new KnowledgeAuthority(identity.Object, "Global");
        
        var provider = new InMemoryKnowledgeProvider("Primary");
        var descriptor = new KnowledgeDescriptor("Test");
        var artifact = await provider.StoreArtifactAsync(descriptor, new List<IKnowledgeRepresentation>(), null, authority);
        
        var authRef = new AuthoritativeReference("Primary", "Artifact", "Test", "latest", authority, "Current");
        await provider.SetAuthoritativeReferenceAsync(authRef, artifact.Reference);

        var service = new KnowledgeService(new[] { provider }, Mock.Of<ITeam<ICuratorClerk>>());

        // Act
        var result = await service.ResolveReferenceAsync(authRef);

        // Assert
        Assert.Equal(artifact.Reference, result.Reference);
    }

    [Fact]
    public async Task SetAuthoritativeReferenceAsync_CallsProvider()
    {
        // Arrange
        var identity = new Mock<IIdentitySubject>();
        var authority = new KnowledgeAuthority(identity.Object, "Global");
        var authRef = new AuthoritativeReference("Primary", "Artifact", "Test", "latest", authority, "Current");
        var targetRef = new KnowledgeReference("Primary", "Artifact", "Test", "1.0.1");

        var provider = new InMemoryKnowledgeProvider("Primary");
        var service = new KnowledgeService(new[] { provider }, Mock.Of<ITeam<ICuratorClerk>>());

        // Act
        await service.SetAuthoritativeReferenceAsync(authRef, targetRef);

        // Assert
        var resolved = await provider.ResolveAuthoritativeReferenceAsync(authRef);
        Assert.Equal(targetRef, resolved);
    }
}
