using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.Artifacts;
using AethericForge.Runtime.Models.Knowledge.Claims;
using AethericForge.Runtime.Models.Knowledge.Compositions;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.Relationships;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Knowledge;

public class KnowledgeModelTests
{
    [Fact]
    public void KnowledgeClaim_ShouldInitializeCorrectly()
    {
        // Arrange
        var reference = new KnowledgeReference("Set", "Kind", "Name", "1.0");
        var descriptor = new KnowledgeDescriptor("Title");
        var asserter = new Mock<IIdentitySubject>().Object;
        var subject = new Mock<IKnowledgeObject>().Object;
        var claimType = "Assertion";

        // Act
        var claim = new KnowledgeClaim(reference, descriptor, asserter, claimType, subject);

        // Assert
        Assert.Same(asserter, claim.Asserter);
        Assert.Equal(claimType, claim.ClaimType);
        Assert.Same(subject, claim.Subject);
        Assert.Empty(claim.Representations);
    }

    [Fact]
    public void KnowledgeRelationship_ShouldInitializeCorrectly()
    {
        // Arrange
        var reference = new KnowledgeReference("Set", "Kind", "Name", "1.0");
        var descriptor = new KnowledgeDescriptor("Title");
        var participants = new[] { new KnowledgeReference("Set", "Kind", "P1", "1.0") };
        var relType = "Dependency";

        // Act
        var rel = new KnowledgeRelationship(reference, descriptor, relType, participants);

        // Assert
        Assert.Equal(relType, rel.RelationshipType);
        Assert.Single(rel.Participants);
    }

    [Fact]
    public void KnowledgeComposition_ShouldInitializeCorrectly()
    {
        // Arrange
        var reference = new KnowledgeReference("Set", "Kind", "Name", "1.0");
        var descriptor = new KnowledgeDescriptor("Title");
        var constituents = new[] { new KnowledgeConstituent(new KnowledgeReference("Set", "Kind", "C1", "1.0"), "Main") };

        // Act
        var comp = new KnowledgeComposition(reference, descriptor, constituents);

        // Assert
        Assert.Single(comp.Constituents);
    }
}
