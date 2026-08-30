using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;
using AethericForge.Runtime.Models.Identity.Directory;
using AethericForge.Runtime.Providers.Identity.InMemory;

namespace AethericForge.Runtime.Tests.Identity.Directory;

public sealed class InMemoryExternalIdentityDirectoryTests : ExternalIdentityDirectoryContractTests
{
    protected override DirectoryFixture CreateFixture() => new InMemoryDirectoryFixture();

    [Fact]
    public async Task SimulatedFailureIsExplicitAndCanBeRestored()
    {
        var fixture = new InMemoryDirectoryFixture();
        var identity = fixture.AddIdentity("member-1", "Ada Lovelace");
        fixture.Instance.SimulateFailure(ExternalDirectoryStatus.Unavailable, "Directory offline.");

        var unavailable = await fixture.Directory.GetIdentityAsync(identity.Reference);
        fixture.Instance.Restore();
        var restored = await fixture.Directory.GetIdentityAsync(identity.Reference);

        Assert.Equal(ExternalDirectoryStatus.Unavailable, unavailable.Status);
        Assert.Equal("Directory offline.", unavailable.FailureReason);
        Assert.Null(unavailable.Value);
        Assert.Equal(ExternalDirectoryStatus.Success, restored.Status);
    }

    [Fact]
    public void UpdatingIdentityReportsChangedPropertiesAgainstStableReference()
    {
        var fixture = new InMemoryDirectoryFixture();
        fixture.AddIdentity("member-1", "Ada Lovelace", true, "ada@example.test");
        var updated = new ExternalIdentity(
            new ExternalIdentityReference(ProviderName, RealmName, "member-1"),
            "Ada Byron",
            false,
            new Dictionary<string, string> { ["email"] = "byron@example.test" });

        var change = fixture.Instance.AddOrUpdateIdentity(updated);

        Assert.Equal("member-1", change.Current.Reference.SubjectId);
        Assert.Equal(["displayName", "isEnabled", "email"], change.ChangedProperties);
    }

    [Fact]
    public void SetGroupMembersRejectsUnknownIdentity()
    {
        var fixture = new InMemoryDirectoryFixture();
        var group = new ExternalGroupReference(ProviderName, RealmName, "members");
        fixture.Instance.AddGroup(group);

        Assert.Throws<ArgumentException>(() => fixture.Instance.SetGroupMembers(
            group,
            [new ExternalIdentityReference(ProviderName, RealmName, "missing")]));
    }

    [Fact]
    public async Task SuccessfulObservationUsesConfiguredFreshnessLifetime()
    {
        var observedAt = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new TestTimeProvider(observedAt);
        var directory = new InMemoryExternalIdentityDirectory(
            ProviderName,
            RealmName,
            timeProvider,
            TimeSpan.FromMinutes(5));
        var identity = new ExternalIdentity(
            new ExternalIdentityReference(ProviderName, RealmName, "member-1"));
        directory.AddOrUpdateIdentity(identity);

        var result = await directory.GetIdentityAsync(identity.Reference);

        Assert.Equal(observedAt, result.ObservedAtUtc);
        Assert.Equal(observedAt.AddMinutes(5), result.FreshUntilUtc);
    }

    private sealed class InMemoryDirectoryFixture : DirectoryFixture
    {
        public InMemoryExternalIdentityDirectory Instance { get; } =
            new(ProviderName, RealmName);

        public override IExternalIdentityDirectory Directory => Instance;

        public override ExternalIdentity AddIdentity(
            string subjectId,
            string displayName,
            bool isEnabled = true,
            string? email = null)
        {
            var properties = email is null
                ? null
                : new Dictionary<string, string> { ["email"] = email };
            var identity = new ExternalIdentity(
                new ExternalIdentityReference(ProviderName, RealmName, subjectId),
                displayName,
                isEnabled,
                properties);
            Instance.AddOrUpdateIdentity(identity);
            return identity;
        }

        public override void AddGroup(
            string groupId,
            params IExternalIdentityReference[] members)
        {
            var group = new ExternalGroupReference(ProviderName, RealmName, groupId);
            Instance.AddGroup(group);
            Instance.SetGroupMembers(group, members);
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
