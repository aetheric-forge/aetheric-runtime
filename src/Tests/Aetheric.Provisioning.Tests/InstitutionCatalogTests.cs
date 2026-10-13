using System.Collections.Immutable;
using System.Security.Cryptography;
using Aetheric.Provisioning.Application;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Persistence;
using Aetheric.Provisioning.Simulation;
using Xunit;

namespace Aetheric.Provisioning.Tests;

public sealed class InstitutionCatalogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "institution-catalog-" + Guid.NewGuid().ToString("N"));
    private FileInstitutionDeploymentStore Store() => new(_directory);

    private static InstitutionDeploymentAttempt Pending(string environment = "test")
    {
        var input = DecisionsFixture.Load();
        input = input with { Bindings = input.Bindings with { Environment = environment } };
        var plan = new ProvisioningPlanner([new SimulatedWorkbenchProvider()]).Plan(input).Plan!;
        var loaded = new LoadedInstitution("Decisions", "A provisioned office", input.Institution, input.Bindings,
            new SourceBundle(new SourceDocument("", input.DefinitionSource), new SourceDocument("", input.BindingsSource)));
        return InstitutionDeploymentRecorder.Begin(loaded, plan, Guid.NewGuid(), DateTimeOffset.UtcNow,
            new DeploymentParent("https://example.com/campus", new string('a', 40), new string('b', 64), ImmutableDictionary<string, string>.Empty));
    }

    [Fact]
    public async Task Pending_and_failed_attempts_do_not_replace_the_successfully_provisioned_revision()
    {
        var pending = Pending();
        await Store().SaveAttemptAsync(pending, default);
        Assert.Null((await Store().ReadAsync(pending.DeploymentId, default))!.Provisioned);
        var success = Success(pending);
        await Store().SaveAttemptAsync(success, default);
        var failed = pending with { RequestId = Guid.NewGuid(), RequestedAtUtc = pending.RequestedAtUtc.AddSeconds(1),
            Version = "2.0.0", Status = InstitutionDeploymentStatus.Failed, CompletedAtUtc = DateTimeOffset.UtcNow,
            Issues = ["operation.failed"] };
        await Store().SaveAttemptAsync(failed, default);
        var fresh = (await Store().ReadAsync(pending.DeploymentId, default))!;
        Assert.Equal("1.0.0", fresh.Provisioned!.Version);
        Assert.Equal("2.0.0", fresh.LatestAttempt.Version);
        Assert.Equal(InstitutionDeploymentStatus.Failed, fresh.LatestAttempt.Status);
        Assert.Single(await Store().ListAsync("test", new string('b', 64), default));
        Assert.Empty(await Store().ListAsync("production", null, default));
    }

    [Fact]
    public async Task A_delayed_old_result_does_not_replace_a_newer_attempt()
    {
        var old = Pending();
        var newer = old with { RequestId = Guid.NewGuid(), RequestedAtUtc = old.RequestedAtUtc.AddSeconds(1) };
        await Store().SaveAttemptAsync(newer, default);
        await Store().SaveAttemptAsync(Success(old), default);
        var record = (await Store().ReadAsync(old.DeploymentId, default))!;
        Assert.Equal(newer.RequestId, record.LatestAttempt.RequestId);
        Assert.Null(record.Provisioned);
    }

    [Fact]
    public async Task Retry_cannot_change_an_approved_plan_under_the_same_request_id()
    {
        var attempt = Pending();
        await Store().SaveAttemptAsync(attempt, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().SaveAttemptAsync(attempt with { PlanId = "different" }, default));
    }

    [Fact]
    public async Task Catalog_contains_secret_references_but_no_secret_values()
    {
        var secrets = new EncryptedFileSecretStore(Path.Combine(_directory, "secrets"), RandomNumberGenerator.GetBytes(32));
        var reference = await secrets.GetOrCreateAsync("test", "library", default);
        var value = await secrets.ReadAsync(reference, default);
        var pending = Pending();
        var resourceId = pending.Resources.Keys.First();
        var outcomes = ImmutableArray.Create(new StepOutcome("owned:" + resourceId, OutcomeStatus.Succeeded, "resource.created", [reference]));
        var completed = InstitutionDeploymentRecorder.Complete(pending, new RunResult(pending.PlanId, outcomes));
        await Store().SaveAttemptAsync(completed, default);
        var text = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(_directory, "*.institution.json")));
        Assert.DoesNotContain(value, text);
        Assert.Contains(reference.Id, text);
        Assert.Equal(reference, (await Store().ReadAsync(pending.DeploymentId, default))!.Provisioned!.Resources[resourceId].Secrets.Single());
    }

    [Fact]
    public async Task Operation_lease_serializes_processes_and_supports_cancellation()
    {
        var attempt = Pending();
        await using (var lease = await Store().AcquireAsync(attempt.DeploymentId, default))
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store().AcquireAsync(attempt.DeploymentId, cancellation.Token));
        }
        await using var next = await Store().AcquireAsync(attempt.DeploymentId, default);
    }

    [Fact]
    public async Task Corrupt_catalog_data_is_rejected_instead_of_reported_as_empty_inventory()
    {
        var pending = Pending();
        await Store().SaveAttemptAsync(pending, default);
        await File.WriteAllTextAsync(Assert.Single(Directory.GetFiles(_directory, "*.institution.json")), "{\"Version\":99}");
        await Assert.ThrowsAsync<InvalidDataException>(() => Store().ListAsync(null, null, default));
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    private static InstitutionDeploymentAttempt Success(InstitutionDeploymentAttempt pending) => pending with
    {
        Status = InstitutionDeploymentStatus.Succeeded, CompletedAtUtc = DateTimeOffset.UtcNow,
        Outcomes = pending.Resources.Keys.Select(id => new StepOutcome("owned:" + id, OutcomeStatus.Succeeded, "resource.created", [])).ToImmutableArray()
    };
}
