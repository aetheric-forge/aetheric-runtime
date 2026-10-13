using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheric.Provisioning.Engine;

namespace Aetheric.Provisioning.Persistence;

/// <summary>Atomic, private catalog snapshots. Failed retries retain the last provisioned revision.</summary>
public sealed class FileInstitutionDeploymentStore : IInstitutionDeploymentStore, IInstitutionDeploymentLock
{
    private readonly FileStorage _files;
    private readonly string _directory;
    private sealed record Snapshot(int Version, InstitutionDeployment Deployment);

    public FileInstitutionDeploymentStore(string directory)
    {
        _files = new FileStorage(directory);
        _directory = Path.GetFullPath(directory);
    }

    public async Task<IAsyncDisposable> AcquireAsync(string deploymentId, CancellationToken ct)
    {
        ValidateId(deploymentId);
        return await FileStorage.LockAsync(_files.PathFor(deploymentId, ".operation.lock"), ct);
    }

    public async Task SaveAttemptAsync(InstitutionDeploymentAttempt attempt, CancellationToken ct)
    {
        Validate(attempt);
        await using var lease = await FileStorage.LockAsync(_files.PathFor(attempt.DeploymentId, ".lock"), ct);
        var prior = await ReadAsync(attempt.DeploymentId, ct);
        // A delayed result from an older request must not displace a newer revision.
        if (prior is not null && prior.LatestAttempt.RequestedAtUtc > attempt.RequestedAtUtc) return;
        if (prior is not null && prior.LatestAttempt.RequestId == attempt.RequestId
            && prior.LatestAttempt.PlanId != attempt.PlanId)
            throw new InvalidOperationException("A deployment request cannot change its approved plan.");
        if (prior?.LatestAttempt.RequestId == attempt.RequestId
            && prior.LatestAttempt.Status != InstitutionDeploymentStatus.Pending
            && attempt.Status == InstitutionDeploymentStatus.Pending) return;
        var provisioned = attempt.Status == InstitutionDeploymentStatus.Succeeded ? attempt : prior?.Provisioned;
        var snapshot = new Snapshot(1, new(attempt.DeploymentId, attempt, provisioned));
        await FileStorage.WriteAsync(_files.PathFor(attempt.DeploymentId, ".institution.json"),
            JsonSerializer.SerializeToUtf8Bytes(snapshot), ct);
    }

    public async Task<InstitutionDeployment?> ReadAsync(string deploymentId, CancellationToken ct)
    {
        ValidateId(deploymentId);
        try { return Read(await File.ReadAllBytesAsync(_files.PathFor(deploymentId, ".institution.json"), ct), deploymentId); }
        catch (FileNotFoundException) { return null; }
    }

    public async Task<IReadOnlyList<InstitutionDeployment>> ListAsync(string? environment, string? parentDeploymentId, CancellationToken ct)
    {
        var results = new List<InstitutionDeployment>();
        foreach (var file in Directory.EnumerateFiles(_directory, "*.institution.json"))
        {
            ct.ThrowIfCancellationRequested();
            var deployment = Read(await File.ReadAllBytesAsync(file, ct), null);
            // Inventory uses the provisioned revision when one exists, even after a failed update.
            var revision = deployment.Provisioned ?? deployment.LatestAttempt;
            if ((environment is null || revision.Environment == environment)
                && (parentDeploymentId is null || revision.Parent?.DeploymentId == parentDeploymentId))
                results.Add(deployment);
        }
        return results.OrderBy(x => x.DeploymentId, StringComparer.Ordinal).ToArray();
    }

    private static InstitutionDeployment Read(byte[] bytes, string? expectedId)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<Snapshot>(bytes);
            if (snapshot?.Version != 1 || snapshot.Deployment is not { } deployment
                || deployment.DeploymentId != deployment.LatestAttempt?.DeploymentId
                || expectedId is not null && deployment.DeploymentId != expectedId)
                throw new InvalidDataException("Invalid or unsupported institution catalog snapshot.");
            Validate(deployment.LatestAttempt);
            if (deployment.Provisioned is { } provisioned)
            {
                Validate(provisioned);
                if (provisioned.DeploymentId != deployment.DeploymentId || provisioned.Status != InstitutionDeploymentStatus.Succeeded)
                    throw new InvalidDataException("Invalid provisioned revision.");
            }
            return deployment;
        }
        catch (JsonException) { throw new InvalidDataException("Invalid institution catalog JSON."); }
        catch (ArgumentException) { throw new InvalidDataException("Invalid institution catalog identity."); }
    }

    private static void ValidateId(string id)
    {
        if (id is null || !Regex.IsMatch(id, "\\A[0-9a-f]{64}\\z"))
            throw new ArgumentException("A valid institution deployment ID is required.");
    }

    private static void Validate(InstitutionDeploymentAttempt attempt)
    {
        ValidateId(attempt.DeploymentId);
        if (attempt.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(attempt.InstitutionId)
            || string.IsNullOrWhiteSpace(attempt.Environment) || string.IsNullOrWhiteSpace(attempt.Version)
            || string.IsNullOrWhiteSpace(attempt.Name) || attempt.DefinitionSource is null || attempt.BindingsSource is null
            || attempt.DeploymentId != InstitutionDeploymentAttempt.Identity(attempt.DefinitionSource.Repository, attempt.InstitutionId, attempt.Environment)
            || string.IsNullOrWhiteSpace(attempt.PlanId) || !Enum.IsDefined(attempt.Status)
            || (attempt.Status == InstitutionDeploymentStatus.Pending) != (attempt.CompletedAtUtc is null)
            || attempt.Outcomes.IsDefault || attempt.Issues.IsDefault || attempt.Resources is null
            || attempt.Resources.Any(x => x.Value is null || x.Key != x.Value.Id || x.Value.Secrets.IsDefault || x.Value.Settings is null
                || x.Value.Connection is null || x.Value.Secrets.Any(s => s is null || string.IsNullOrWhiteSpace(s.Id)))
            || attempt.Outcomes.Any(x => x is null || !Enum.IsDefined(x.Status) || x.Secrets.IsDefault)
            || attempt.Status == InstitutionDeploymentStatus.Succeeded && (attempt.Outcomes.Any(x => !x.IsSuccessful)
                || attempt.Resources.Keys.Any(id => !attempt.Outcomes.Any(x => x.StepId == "owned:" + id && x.IsSuccessful))))
            throw new InvalidDataException("Invalid institution deployment attempt.");
    }
}
