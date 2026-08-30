using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;
using AethericForge.Runtime.Models.Identity.Directory;

namespace AethericForge.Runtime.Tests.Identity.Directory;

public sealed class ExternalDirectoryModelTests
{
    [Fact]
    public void ReferencesNormalizeProviderRealmAndIdentifiers()
    {
        var identity = new ExternalIdentityReference(" provider ", " realm ", " subject ");
        var group = new ExternalGroupReference(" provider ", " realm ", " group ");

        Assert.Equal("provider", identity.Provider);
        Assert.Equal("realm", identity.Realm);
        Assert.Equal("subject", identity.SubjectId);
        Assert.Equal("group", group.GroupId);
    }

    [Fact]
    public void IdentityPropertiesAreNormalizedAndCaseInsensitive()
    {
        var identity = new ExternalIdentity(
            new ExternalIdentityReference("provider", "realm", "subject"),
            " Display Name ",
            properties: new Dictionary<string, string> { [" Email "] = " user@example.test " });

        Assert.Equal("Display Name", identity.DisplayName);
        Assert.Equal("user@example.test", identity.Properties["email"]);
    }

    [Fact]
    public void SuccessfulResultRequiresAValue()
    {
        Assert.Throws<ArgumentException>(() =>
            ExternalDirectoryResult<string>.Success(null!, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FailureCannotMasqueradeAsSuccess()
    {
        Assert.Throws<ArgumentException>(() =>
            ExternalDirectoryResult<string>.Failure(
                ExternalDirectoryStatus.Success,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FreshnessCannotEndBeforeObservation()
    {
        var observedAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExternalDirectoryResult<string>.Success(
                "value",
                observedAt,
                observedAt.AddSeconds(-1)));
    }
}
