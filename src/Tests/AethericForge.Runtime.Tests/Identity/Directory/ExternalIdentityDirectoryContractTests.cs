using AethericForge.Runtime.Abstractions.Interfaces.Identity.Directory;
using AethericForge.Runtime.Models.Identity.Directory;

namespace AethericForge.Runtime.Tests.Identity.Directory;

public abstract class ExternalIdentityDirectoryContractTests
{
    protected const string ProviderName = "test-directory";
    protected const string RealmName = "test-realm";

    protected abstract DirectoryFixture CreateFixture();

    [Fact]
    public async Task GetIdentity_ReturnsCurrentIdentityWithFreshness()
    {
        var fixture = CreateFixture();
        var identity = fixture.AddIdentity("member-1", "Ada Lovelace", true, "ada@example.test");

        var result = await fixture.Directory.GetIdentityAsync(identity.Reference);

        Assert.Equal(ExternalDirectoryStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("member-1", result.Value.Reference.SubjectId);
        Assert.Equal("Ada Lovelace", result.Value.DisplayName);
        Assert.Equal("ada@example.test", result.Value.Properties["email"]);
        Assert.True(result.FreshUntilUtc >= result.ObservedAtUtc);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task GetIdentity_DistinguishesNotFoundFromProviderFailure()
    {
        var fixture = CreateFixture();
        var missing = new ExternalIdentityReference(ProviderName, RealmName, "missing");

        var result = await fixture.Directory.GetIdentityAsync(missing);

        Assert.Equal(ExternalDirectoryStatus.NotFound, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetIdentity_RejectsReferenceFromAnotherRealm()
    {
        var fixture = CreateFixture();
        var foreign = new ExternalIdentityReference(ProviderName, "another-realm", "member-1");

        var result = await fixture.Directory.GetIdentityAsync(foreign);

        Assert.Equal(ExternalDirectoryStatus.Untrusted, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetGroups_ReturnsOnlyDirectlyConfiguredMemberships()
    {
        var fixture = CreateFixture();
        var identity = fixture.AddIdentity("member-1", "Ada Lovelace");
        fixture.AddGroup("members", identity.Reference);
        fixture.AddGroup("maintainers", identity.Reference);
        fixture.AddGroup("unrelated");

        var result = await fixture.Directory.GetGroupsAsync(identity.Reference);

        Assert.Equal(ExternalDirectoryStatus.Success, result.Status);
        Assert.Equal(
            ["maintainers", "members"],
            result.Value!.Select(group => group.GroupId).ToArray());
    }

    [Fact]
    public async Task GetGroupMembers_ReturnsStableIdentitiesInDeterministicOrder()
    {
        var fixture = CreateFixture();
        var second = fixture.AddIdentity("member-2", "Grace Hopper");
        var first = fixture.AddIdentity("member-1", "Ada Lovelace");
        fixture.AddGroup("members", second.Reference, first.Reference);

        var result = await fixture.Directory.GetGroupMembersAsync(
            new ExternalGroupReference(ProviderName, RealmName, "members"));

        Assert.Equal(ExternalDirectoryStatus.Success, result.Status);
        Assert.Equal(
            ["member-1", "member-2"],
            result.Value!.Select(identity => identity.Reference.SubjectId).ToArray());
    }

    [Fact]
    public async Task DisabledIdentitiesRemainRepresentedAsDisabledDirectoryFacts()
    {
        var fixture = CreateFixture();
        var identity = fixture.AddIdentity("member-1", "Ada Lovelace", false);
        fixture.AddGroup("members", identity.Reference);

        var result = await fixture.Directory.GetGroupMembersAsync(
            new ExternalGroupReference(ProviderName, RealmName, "members"));

        Assert.Equal(ExternalDirectoryStatus.Success, result.Status);
        Assert.Single(result.Value!);
        Assert.False(result.Value!.Single().IsEnabled);
    }

    [Fact]
    public async Task CancellationIsObservedBeforeDirectoryAccess()
    {
        var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Directory.GetIdentityAsync(
                new ExternalIdentityReference(ProviderName, RealmName, "member-1"),
                cancellation.Token));
    }

    protected abstract class DirectoryFixture
    {
        public abstract IExternalIdentityDirectory Directory { get; }

        public abstract ExternalIdentity AddIdentity(
            string subjectId,
            string displayName,
            bool isEnabled = true,
            string? email = null);

        public abstract void AddGroup(
            string groupId,
            params IExternalIdentityReference[] members);
    }
}
