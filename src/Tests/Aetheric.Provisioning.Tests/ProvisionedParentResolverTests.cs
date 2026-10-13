using System.Collections.Immutable;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Persistence;
using Aetheric.Provisioning.Workbench.Redis;
using Aetheric.Provisioning.Worker;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class ProvisionedParentResolverTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "provisioned-parents-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Parent_capabilities_come_from_nearest_provisioned_owners_instead_of_request_locations()
    {
        var store = new FileInstitutionDeploymentStore(_directory);
        var university = Record("university", null, Resource("registry", "identity", "keycloak", "realm", "university-members"));
        var campus = Record("campus", university.DeploymentId,
            Resource("library", "knowledge", "mongodb", "database", "campus-library"),
            Resource("workbench", "staging", "workbench", "stage", "campus-workbench"));
        var faculty = Record("faculty", campus.DeploymentId, Resource("registry", "identity", "keycloak", "realm", "faculty-members"));
        await store.SaveAttemptAsync(university, default);
        await store.SaveAttemptAsync(campus, default);
        await store.SaveAttemptAsync(faculty, default);
        var requested = new ParentIdentity("https://wrong.example", "wrong-revision",
            new Dictionary<string, string> { ["IRegistrar"] = "wrong-realm" }) { DeploymentId = faculty.DeploymentId };
        var resolved = (await ProvisionedParentResolver.ResolveAsync(store, requested, default))!;
        Assert.Equal("faculty-members", resolved.ResourceLocations["IRegistrar"]);
        Assert.Equal("campus", resolved.ResourceLocations["ILibrary"]);
        Assert.True(WorkbenchParentLocation.TryParse(resolved.ResourceLocations["IWorkbench"], out var location));
        Assert.Equal("campus-workbench", location!.Stage);
        Assert.Equal("campus", location.Institution);
        Assert.Equal(faculty.DefinitionSource.Repository, resolved.Repository);
        Assert.Equal(faculty.DefinitionSource.Commit, resolved.Revision);
    }

    [Fact]
    public async Task Missing_or_unprovisioned_parent_is_not_treated_as_an_available_capability()
    {
        var store = new FileInstitutionDeploymentStore(_directory);
        var parent = new ParentIdentity("https://example.com", "revision", new Dictionary<string, string>()) { DeploymentId = new string('a', 64) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProvisionedParentResolver.ResolveAsync(store, parent, default));
    }

    [Fact]
    public async Task Missing_ancestor_and_containment_cycles_fail_closed()
    {
        var store = new FileInstitutionDeploymentStore(_directory);
        var parent = Record("campus", new string('a', 64));
        await store.SaveAttemptAsync(parent, default);
        var requested = new ParentIdentity("https://example.com", "revision", new Dictionary<string, string>()) { DeploymentId = parent.DeploymentId };
        await Assert.ThrowsAsync<InvalidDataException>(() => ProvisionedParentResolver.ResolveAsync(store, requested, default));
        await store.SaveAttemptAsync(parent with { Parent = parent.Parent! with { DeploymentId = parent.DeploymentId } }, default);
        await Assert.ThrowsAsync<InvalidDataException>(() => ProvisionedParentResolver.ResolveAsync(store, requested, default));
    }

    private static ProvisionedResource Resource(string id, string category, string provider, string key, string value) =>
        new(id, category, provider, new Dictionary<string, string> { [key] = value }.ToImmutableDictionary(), []);

    private static InstitutionDeploymentAttempt Record(string id, string? parent, params ProvisionedResource[] resources)
    {
        var source = new SourceProvenance("https://example.com/" + id, new string('a', 40), "institution.yaml", new string('b', 64));
        return new InstitutionDeploymentAttempt(InstitutionDeploymentAttempt.Identity(source.Repository, id, "test"), Guid.NewGuid(), id,
            id, "Test institution", "1.0.0", "test", parent is null ? null : new DeploymentParent("https://example.com", new string('a', 40), parent, ImmutableDictionary<string, string>.Empty),
            source, source, new string('c', 64), InstitutionDeploymentStatus.Succeeded, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            resources.ToImmutableDictionary(x => x.Id), resources.Select(x => new StepOutcome("owned:" + x.Id, OutcomeStatus.Succeeded, "resource.created", [])).ToImmutableArray(), []);
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
