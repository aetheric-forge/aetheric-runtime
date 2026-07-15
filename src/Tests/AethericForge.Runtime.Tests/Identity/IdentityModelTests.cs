using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Subjects;
using AethericForge.Runtime.Models.Identity.Primitives;
using AethericForge.Runtime.Models.Identity.Trust;
using Moq;
using Xunit;

namespace AethericForge.Runtime.Tests.Identity;

public class IdentityModelTests
{
    [Fact]
    public void IdentityAttestation_ShouldInitializeCorrectly()
    {
        // Arrange
        var type = "EmailVerified";
        var value = "true";
        var signature = new byte[] { 1, 2, 3 };
        var algorithm = "RSA256";
        var issuer = "IdentityServer";

        // Act
        var attestation = new IdentityAttestation(type, value, signature, algorithm, issuer);

        // Assert
        Assert.Equal(type, attestation.Type);
        Assert.Equal(value, attestation.Value);
        Assert.Equal(signature, attestation.Signature);
        Assert.Equal(algorithm, attestation.Algorithm);
        Assert.Equal(issuer, attestation.Issuer);
    }

    [Fact]
    public void IdentityIdentifier_ShouldInitializeCorrectly()
    {
        // Arrange
        var subjectId = "user123";
        var scheme = IdentityScheme.OpenIdConnect;

        // Act
        var identifier = new IdentityIdentifier(subjectId, scheme);

        // Assert
        Assert.Equal(subjectId, identifier.SubjectId);
        Assert.Equal(scheme, identifier.Scheme);
    }

    [Fact]
    public void IdentityLink_ShouldInitializeCorrectly()
    {
        // Arrange
        var primary = new IdentityIdentifier("p1", IdentityScheme.Local);
        var linked = new IdentityIdentifier("l1", IdentityScheme.OAuth2);
        var linkType = "SocialAccount";

        // Act
        var link = new IdentityLink(primary, linked, linkType);

        // Assert
        Assert.Same(primary, link.Primary);
        Assert.Same(linked, link.Linked);
        Assert.Equal(linkType, link.LinkType);
    }

    [Fact]
    public void TrustRelationship_ShouldInitializeCorrectly()
    {
        // Arrange
        var relType = "Delegate";
        var trustor = new Mock<IIdentitySubject>().Object;
        var trustee = new Mock<IIdentitySubject>().Object;
        var established = DateTimeOffset.UtcNow;

        // Act
        var rel = new TrustRelationship(relType, trustor, trustee, established);

        // Assert
        Assert.Equal(relType, rel.RelationshipType);
        Assert.Same(trustor, rel.Trustor);
        Assert.Same(trustee, rel.Trustee);
        Assert.Equal(established, rel.EstablishedAtUtc);
    }
}
