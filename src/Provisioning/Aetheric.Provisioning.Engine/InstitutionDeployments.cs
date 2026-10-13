using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aetheric.Provisioning.Engine;

public enum InstitutionDeploymentStatus { Pending, Succeeded, Failed, Cancelled }
public enum ConsumerDeliveryStatus { Ready, Failed }
public sealed record ConsumerDelivery(ConsumerDeliveryStatus Status, string Profile, string Revision,
    ImmutableDictionary<string, SecretReference> Credentials, ImmutableArray<string> Issues);

// Credentials are opaque references. Neither the catalog nor its query contract contains root credentials.
public sealed record ProvisionedResource(string Id, string Category, string Provider,
    ImmutableDictionary<string, string> Settings, ImmutableArray<SecretReference> Secrets)
{
    public ImmutableDictionary<string, string> Connection { get; init; } = ImmutableDictionary<string, string>.Empty;
}
public sealed record DeploymentParent(string Repository, string Revision, string? DeploymentId,
    ImmutableDictionary<string, string> ResourceLocations);
public sealed record InstitutionDeploymentAttempt(string DeploymentId, Guid RequestId,
    string InstitutionId, string Name, string Description, string Version, string Environment,
    DeploymentParent? Parent, SourceProvenance DefinitionSource, SourceProvenance BindingsSource,
    string PlanId, InstitutionDeploymentStatus Status, DateTimeOffset RequestedAtUtc,
    DateTimeOffset? CompletedAtUtc, ImmutableDictionary<string, ProvisionedResource> Resources,
    ImmutableArray<StepOutcome> Outcomes, ImmutableArray<string> Issues)
{
    public ConsumerDelivery? Consumer { get; init; }
    // Existing providers support one definition ID per environment/repository, not multiple
    // tenant instances. Version, display name, commit and request ID never change this identity.
    public static string Identity(string repository, string institutionId, string environment) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new[] { repository, institutionId, environment })));
}

public sealed record InstitutionDeployment(string DeploymentId, InstitutionDeploymentAttempt LatestAttempt,
    InstitutionDeploymentAttempt? Provisioned);

public interface IInstitutionDeploymentStore
{
    Task SaveAttemptAsync(InstitutionDeploymentAttempt attempt, CancellationToken cancellationToken);
    Task<InstitutionDeployment?> ReadAsync(string deploymentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InstitutionDeployment>> ListAsync(string? environment, string? parentDeploymentId,
        CancellationToken cancellationToken);
}

public interface IInstitutionDeploymentLock
{
    Task<IAsyncDisposable> AcquireAsync(string deploymentId, CancellationToken cancellationToken);
}
