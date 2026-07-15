using AethericForge.Runtime.Models.Identity.Primitives;
using Xunit;

namespace AethericForge.Runtime.Tests.Identity;

public class IdentityClaimTests
{
    [Fact]
    public void Constructor_Sets_Properties()
    {
        // Arrange
        var type = "test-type";
        var value = "test-value";
        var issuer = "test-issuer";
        var issuedAtUtc = DateTimeOffset.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddDays(1);

        // Act
        var claim = new IdentityClaim(type, value, issuer, issuedAtUtc, expiresAtUtc);

        // Assert
        Assert.Equal(type, claim.Type);
        Assert.Equal(value, claim.Value);
        Assert.Equal(issuer, claim.Issuer);
        Assert.Equal(issuedAtUtc, claim.IssuedAtUtc);
        Assert.Equal(expiresAtUtc, claim.ExpiresAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_When_Type_Is_Missing(string type)
    {
        Assert.Throws<ArgumentException>(() => new IdentityClaim(type, "value"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Throws_When_Value_Is_Missing(string value)
    {
        Assert.Throws<ArgumentException>(() => new IdentityClaim("type", value));
    }

    [Fact]
    public void Constructor_Trims_Type_And_Value()
    {
        // Act
        var claim = new IdentityClaim("  type  ", "  value  ");

        // Assert
        Assert.Equal("type", claim.Type);
        Assert.Equal("value", claim.Value);
    }

    [Fact]
    public void Constructor_Throws_When_Type_Too_Long()
    {
        var longType = new string('a', 129);
        Assert.Throws<ArgumentOutOfRangeException>(() => new IdentityClaim(longType, "value"));
    }

    [Fact]
    public void Constructor_Throws_When_Value_Too_Long()
    {
        var longValue = new string('a', 2049);
        Assert.Throws<ArgumentOutOfRangeException>(() => new IdentityClaim("type", longValue));
    }

    [Fact]
    public void Constructor_Throws_When_Issuer_Too_Long()
    {
        var longIssuer = new string('a', 257);
        Assert.Throws<ArgumentOutOfRangeException>(() => new IdentityClaim("type", "value", longIssuer));
    }
}
