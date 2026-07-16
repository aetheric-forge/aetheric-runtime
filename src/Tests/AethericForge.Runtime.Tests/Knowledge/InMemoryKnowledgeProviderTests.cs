using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Representations;
using AethericForge.Runtime.Models.Knowledge.Artifacts;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.References;
using AethericForge.Runtime.Providers.Knowledge.InMemory;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Knowledge;

public class InMemoryKnowledgeProviderTests
{
    private readonly InMemoryKnowledgeProvider _provider = new(Scheme);
    private const string Scheme = "test-scheme";

    [Fact]
    public async Task StoreAndGetArtifact_WorkCorrectly()
    {
        // Arrange
        var descriptor = new KnowledgeDescriptor("Test Artifact");
        var identity = new Mock<IIdentitySubject>();
        identity.Setup(i => i.SubjectId).Returns("Author1");
        var authority = new KnowledgeAuthority(identity.Object, "Global");

        // Act
        var artifact = await _provider.StoreArtifactAsync(descriptor, new List<IKnowledgeRepresentation>(), null, authority);
        var retrieved = await _provider.GetArtifactAsync(artifact.Reference);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(descriptor.Title, retrieved.Descriptor.Title);
        Assert.Equal(authority.Identity.SubjectId, retrieved.Authority?.Identity.SubjectId);
    }

    [Fact]
    public async Task AuthoritativeReferences_WorkCorrectly()
    {
        // Arrange
        var identity = new Mock<IIdentitySubject>();
        identity.Setup(i => i.SubjectId).Returns("Author1");
        var authority = new KnowledgeAuthority(identity.Object, "Global");
        
        var artifact = await _provider.StoreArtifactAsync(new KnowledgeDescriptor("V1"), new List<IKnowledgeRepresentation>(), null, authority);
        var authRef = new AuthoritativeReference(Scheme, "Artifact", "Test", "latest", authority, "Current");

        // Act
        await _provider.SetAuthoritativeReferenceAsync(authRef, artifact.Reference);
        var resolved = await _provider.ResolveAuthoritativeReferenceAsync(authRef);

        // Assert
        Assert.Equal(artifact.Reference, resolved);
    }
}
